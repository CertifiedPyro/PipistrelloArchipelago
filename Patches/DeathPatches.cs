using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;
using Object = Il2CppPipistrello.Object;
using Random = UnityEngine.Random;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal class DeathPatches
{
    private static int _currentDeaths;
    private static string _deathCause;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        _currentDeaths = 0;
        _deathCause = null;
    }

    [HarmonyPatch(typeof(Director), nameof(Director.HandleDeath))]
    [HarmonyPrefix]
    public static void HandleDeathPatch()
    {
        // Check if death link is enabled.
        if (!ModSettings.DeathLink.Value)
        {
            return;
        }

        // Check if death came from death link.
        if (Global.State.QueuedDeath != null)
        {
            Global.State.QueuedDeath = null;
            return;
        }

        _currentDeaths += 1;
        if (_currentDeaths < Global.State.DeathLinkAmnesty)
        {
            return;
        }

        // Send death link.
        _currentDeaths = 0;
        var playerName = Global.State.Session.Players.GetPlayerAlias(Global.State.Session.ConnectionInfo.Slot);
        var cause = $"{playerName} {_deathCause ?? "died."}";
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

        var projectile = data.hitbox.obj.TryCast<ObjectEnemyProjectile>();
        if (projectile != null)
        {
            _deathCause = projectile.spriteName switch
            {
                "enemies/beeShoot/bullet" => "a gunner bee's bullet",
                "enemies/cosplayNinja/kunai" => "a ninja's kunai",
                "enemies/cosplayNinja/shuriken" => "a ninja's shuriken",
                "enemies/foodBandit/1_projectile1" or "enemies/foodBandit/1_projectile2" =>
                    "a nacho food bandit's projectile",
                "enemies/foodBandit/2_projectile1" or "enemies/foodBandit/2_projectile2" =>
                    "a guacamole food bandit's projectile",
                "enemies/madameBoss/madameProjectile" => "Madame Pipistrello's projectiles",
                "enemies/swimmer/bubble" => "a swimmer's bubble",
                _ => "a projectile"
            };
            _deathCause = $"died to {_deathCause}.";
            return;
        }

        // Check obj type, since obj.objectDefName might be empty for enemies that spawn in a boss arena.
        _deathCause = data.hitbox.obj?.GetIl2CppType().Name switch
        {
            nameof(EnemyBeeDash) => "a cop bee",
            nameof(EnemyBeeShoot) => "a gunner bee",
            nameof(EnemyCosplayBoss) => "Linkoln",
            nameof(EnemyCosplayBossBoomerang) => "Linkoln's boomerang",
            nameof(EnemyCosplayHelix) => "Bat-Guy",
            nameof(EnemyCosplayMist) => "a ghost",
            nameof(EnemyCosplayNinja) => "a ninja",
            nameof(EnemyCosplayProtect) => "a protector",
            nameof(EnemyCosplaySamurai) => "a samurai",
            nameof(EnemyFairyToxy) => "Toxy",
            nameof(EnemyFoodBanditGuacamole) => "a guacamole food bandit",
            nameof(EnemyFoodBanditNacho) => "a nacho food bandit",
            nameof(EnemyHammerSwinger) => "a hammer swinger",
            nameof(EnemyHockeyPuck) => "a hockey puck",
            nameof(EnemyMadameBoss) => "Madame Pipistrello",
            nameof(EnemyMadameHand) => "Madame Pipistrello's batteries",
            nameof(EnemyMistProjectile) => "a ghost's mist",
            nameof(EnemyRatBoss) => "Don Maretti",
            nameof(EnemyRatDrone) => "an RC car rat",
            nameof(EnemyRatDroneCar) => "an RC car",
            nameof(EnemyRatMelee) => "a melee rat",
            nameof(EnemyRatScooter) => "a scooter rat",
            nameof(EnemyRatShoot) => "a gunner rat",
            nameof(EnemySlime) when data.hitbox.obj.Cast<EnemySlime>().kind == EnemySlime.Kind.Red => "a red slime",
            nameof(EnemySlime) when data.hitbox.obj.Cast<EnemySlime>().kind == EnemySlime.Kind.Blue => "a blue slime",
            nameof(EnemySlime) when data.hitbox.obj.Cast<EnemySlime>().kind == EnemySlime.Kind.Big => "a big slime",
            nameof(EnemySlimeBomb) => "a bomber slime",
            nameof(EnemySlimeDrill) => "a driller slime",
            nameof(EnemySlimePunch) => "a puncher slime",
            nameof(EnemySlimeShield) => "a shield slime",
            nameof(EnemySlimeTycoon) => "the Slime Tycoon",
            nameof(EnemySoccerCurveBall) => "an electric soccer ball",
            nameof(EnemySoccerFireBall) => "a fiery soccer ball",
            nameof(EnemySportsBatter) => "a batter",
            nameof(EnemySportsCarrara) => "Cuca Carrara",
            nameof(EnemySportsCharger) => "Cuca Carrara's football players",
            nameof(EnemySportsHockey) => "a hockey player",
            nameof(EnemySportsSkater) => "a skater",
            nameof(EnemySportsSoccer) => "a soccer player",
            nameof(EnemySwimmer) => "a swimmer",
            nameof(EnemyTurtle) => "a turtle",
            nameof(FireTrail) => "fire",
            nameof(ObjectExplosion) => "an explosion",
            nameof(ObjectGroundSpikeTrap) => "a turtle's spike trap",
            nameof(ObjectPigeon) => "a pigeon",
            nameof(ObjectSpikeRoller) => "a spike roller",
            nameof(ObjectTurtleShell) => "a turtle shell",
            _ => null
        };
        _deathCause = _deathCause != null ? $"died to {_deathCause}." : "died.";
    }
}