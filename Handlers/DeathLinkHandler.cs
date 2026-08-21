using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;
using Object = Il2CppPipistrello.Object;
using Random = UnityEngine.Random;

namespace PipistrelloArchipelago.Handlers;

[HarmonyPatch]
internal static class DeathLinkHandler
{
    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene
    ];

    private static bool _isDeathLinkHandlerEnabled;

    private static bool _queuedDeath;
    private static bool _handlingDeathLinkDeath;

    private static int _currentDeaths;
    private static string _deathCause;

    public static bool IsStarted()
    {
        return _isDeathLinkHandlerEnabled;
    }

    public static void HandleDeathLink(DeathLink deathLink)
    {
        Melon<PipArchMod>.Logger.Msg($"Received death link: {deathLink.Source}, {deathLink.Cause}");
        _queuedDeath = true;
    }

    public static async Task Start()
    {
        _isDeathLinkHandlerEnabled = true;
        try
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded ||
                    !_queuedDeath ||
                    !CanKillPlayer(Global.Director.player.state) ||
                    _handlingDeathLinkDeath)
                {
                    continue;
                }

                Melon<PipArchMod>.Logger.Msg("Killing player...");
                _handlingDeathLinkDeath = true;
                Global.Director.player.Kill();
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {e}");
        }
        finally
        {
            _isDeathLinkHandlerEnabled = false;
            _handlingDeathLinkDeath = false;
        }
    }

    [HarmonyPatch(typeof(Director), nameof(Director.HandleDeath))]
    [HarmonyPrefix]
    public static void HandleDeathPatch()
    {
        if (_queuedDeath)
        {
            _queuedDeath = false;
            _handlingDeathLinkDeath = false;
            return;
        }

        _currentDeaths += 1;
        if (_currentDeaths < Global.State.DeathLinkAmnesty)
        {
            return;
        }

        _currentDeaths = 0;
        var playerName = Global.State.Session.Players.GetPlayerAlias(Global.State.Session.ConnectionInfo.Slot);
        var cause = !string.IsNullOrEmpty(_deathCause) ? _deathCause : "died.";
        cause = $"{playerName} {cause}";
        Melon<PipArchMod>.Logger.Msg($"Sending death link: {cause}");
        Global.State.DeathLinkService.SendDeathLink(new DeathLink(playerName, cause));
    }

    [HarmonyPatch(typeof(Object), nameof(Object.PlayFallInHoleParticle))]
    [HarmonyPostfix]
    public static void FallInHolePatch(Object __instance)
    {
        if (__instance.TryCast<ObjectPlayer>() == null)
        {
            return;
        }

        var causes = new[]
        {
            "fell into a hole.",
            "slipped into a pit.",
            "misjudged a step.",
            "discovered gravity for the first time."
        };
        _deathCause = causes[Random.Range(0, causes.Length)];
    }

    [HarmonyPatch(typeof(Object), nameof(Object.PlayFallInLiquidParticle))]
    [HarmonyPostfix]
    public static void FallInLiquidPatch(Object __instance)
    {
        if (__instance.TryCast<ObjectPlayer>() == null)
        {
            return;
        }

        var causes = new[]
        {
            "fell into some liquid.",
            "drowned.",
            "forgot how to swim.",
            "went for an unscheduled swim."
        };
        _deathCause = causes[Random.Range(0, causes.Length)];
    }

    [HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.OnFallEnd))]
    [HarmonyPostfix]
    public static void FallEndPatch()
    {
        if (Global.Director.player.life > 0)
        {
            _deathCause = null;
        }
    }

    [HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.PlayerReceiveHit))]
    [HarmonyPostfix]
    public static void PlayerReceiveHitPatch(HitboxManager.OnReceiveHitData data)
    {
        Melon<PipArchMod>.Logger.Msg(
            $"PlayerReceiveHit: {Global.Director.player.life}, {data.hitbox.id}, {data.hitbox.type}");
        Melon<PipArchMod>.Logger.Msg(
            $"{data.hitbox.obj != null}, {data.hitbox.obj?.objectDefName}, {data.hitbox.obj?.GetIl2CppType().Name}");
        Melon<PipArchMod>.Logger.Msg(
            $"{data.hitbox.obj?.TryCast<ObjectEnemyProjectile>()?.reflectTarget?.objectDefName}, {data.hitbox.obj?.TryCast<ObjectEnemyProjectile>()?.spriteName}");

        if (Global.Director.player.life > 0 || data.hitbox.obj == null)
        {
            return;
        }

        var vehicleCauses = new[]
        {
            "got ran over by a vehicle.",
            "forgot to look both ways.",
            "was caught jaywalking.",
            "turned into a speed bump."
        };
        if (data.hitbox.obj.TryCast<ObjectVehicle>() != null)
        {
            _deathCause = vehicleCauses[Random.Range(0, vehicleCauses.Length)];
            return;
        }

        _deathCause = data.hitbox.obj?.GetIl2CppType().Name switch
        {
            "EnemyHockeyPuck" => "a hockey puck", // ✅
            "EnemyMadameHand" => "Madame Pipistrello's batteries",
            "EnemyMistProjectile" => "a ghost's mist", // ✅
            "EnemyRatDroneCar" => "an RC car", // ✅
            "EnemySoccerCurveBall" => "an electric soccer ball", // ✅
            "EnemySoccerFireBall" => "a fiery soccer ball", // ✅
            "FireTrail" => "fire", // ✅
            "ObjectExplosion" => "an explosion", // ✅
            "ObjectPigeon" => "a pigeon", // ✅
            "ObjectSpikeRoller" => "a spike roller",
            "ObjectTurtleShell" => "a turtle shell", // ✅
            _ => null
        };
        if (_deathCause != null)
        {
            _deathCause = $"died to {_deathCause}.";
            return;
        }

        var projectile = data.hitbox.obj.TryCast<ObjectEnemyProjectile>();
        if (projectile != null)
        {
            _deathCause = projectile.spriteName switch
            {
                "enemies/beeShoot/bullet" => "a gunner bee's bullet", // ✅
                "enemies/cosplayNinja/kunai" => "a ninja's kunai", // ✅
                "enemies/cosplayNinja/shuriken" => "a ninja's shuriken", // ✅
                "enemies/foodBandit/1_projectile1" or "enemies/foodBandit/1_projectile2" =>
                    "a nacho food bandit's projectile", // ✅
                "enemies/foodBandit/2_projectile1" or "enemies/foodBandit/2_projectile2" =>
                    "a guacamole food bandit's projectile", // ✅
                "enemies/madameBoss/madameProjectile" => "Madame Pipistrello's projectiles",
                "enemies/swimmer/bubble" => "a swimmer's bubble", // ✅
                _ => "a projectile"
            };
            _deathCause = $"died to {_deathCause}.";
            return;
        }

        _deathCause = data.hitbox.obj.objectDefName switch
        {
            "beeDash" => "a cop bee", // ✅
            "beeShoot" => "a gunner bee", // ✅
            "cosplayBoss" => "Linkoln",  // ✅
            "cosplayHelix" => "Bat-Guy", // ✅
            "cosplayMist1" or "cosplayMist2" => "a ghost", // ✅
            "cosplayNinja" => "a ninja", // ✅
            "cosplayProtect" => "a protector", // ✅
            "cosplaySamurai" => "a samurai", // ✅
            "fairyToxy" => "Toxy",
            "foodBandit1" => "a nacho food bandit", // ✅
            "foodBandit2" => "a guacamole food bandit", // ✅
            "groundSpikeTrap" => "a turtle's spike trap",
            "hammerSwinger" => "a hammer swinger", // ✅
            "madameBoss" => "Madame Pipistrello",
            "ratBoss" => "Don Maretti",
            "ratDrone" => "an RC car rat", // Rethink name
            "ratMelee" => "an melee rat", // ✅
            "ratScooter" => "a scooter rat", // ✅
            "ratShoot" => "a gunner rat",
            "slimeBig" => "a big slime", // ✅
            "slimeBlue" => "a blue slime", // ✅
            "slimeBomb" => "a bomber slime",
            "slimeDrill" => "a driller slime", // ✅
            "slimePunch" => "a puncher slime", // ✅
            "slimeRed" => "a red slime", // ✅
            "slimeShield" => "a shield slime", // ✅
            "slimeTycoon" => "the Slime Tycoon",
            "sportsBatter" => "a batter", // ✅
            "sportsCarrara" => "Cuca Carrara",
            "sportsCharger" => "Cuca Carrara's football players", // ✅
            "sportsHockey" => "a hockey player", // ✅
            "sportsSkater" => "a skater", // ✅
            "sportsSoccer" => "a soccer player", // ✅
            "swimmer" => "a swimmer",
            "turtle" => "a turtle", // ✅
            _ => null
        };
        _deathCause = _deathCause != null ? $"died to {_deathCause}." : "died.";
    }

    private static bool CanKillPlayer(ObjectPlayer.State state)
    {
        // Kill if player is not in cutscene, not in menu, and not in dialogue.
        return !InvalidStates.Contains(state)
               && Global.Director.uiDialog == null
               && Global.Director.dialoguePanel?.IsOver() != false;
    }
}