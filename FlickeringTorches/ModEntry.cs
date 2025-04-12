using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using GenericModConfigMenu;

namespace FlickeringTorches
{
    public class ModConfig
    {
        public float RadiusJitterAmount { get; set; } = 0.15f;
        public float ColorJitterAmount { get; set; } = 0.2f;
        public float RadiusChangeSpeed { get; set; } = 0.05f;
        public float ColorChangeSpeed { get; set; } = 0.03f;
        public float BigFlickerChance { get; set; } = 0.02f;
        public float MinBigFlickerIntensity { get; set; } = 0.5f;
        public float MaxBigFlickerIntensity { get; set; } = 1.0f;
    }

    public class ModEntry : Mod
    {
        public static IMonitor SMonitor;
        public static ModConfig Config;
        public static Dictionary<string, FlickerData> FlickeringLights = new Dictionary<string, FlickerData>();
        public static Random GlobalRandom = new Random();

        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.PatchAll();

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnUpdateTicked(object sender, EventArgs e)
        {
            foreach (var flickerData in FlickeringLights.Values)
            {
                flickerData.Update();
            }
        }

        private void OnGameLaunched(object sender, EventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
                );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Radius Jitter",
                tooltip: () => "How much the light size randomly varies (0.01 = 1% to 0.5 = 50%)",
                getValue: () => Config.RadiusJitterAmount,
                setValue: value => Config.RadiusJitterAmount = value,
                min: 0.01f,
                max: 0.5f,
                interval: 0.01f
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Color Jitter",
                tooltip: () => "How much the color randomly shifts (0.05 = 5% to 0.5 = 50%)",
                getValue: () => Config.ColorJitterAmount,
                setValue: value => Config.ColorJitterAmount = value,
                min: 0.05f,
                max: 0.5f,
                interval: 0.01f
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Big Flicker Chance",
                tooltip: () => "Chance for dramatic flickers each frame (0 = never, 0.1 = 10%)",
                getValue: () => Config.BigFlickerChance,
                setValue: value => Config.BigFlickerChance = value,
                min: 0f,
                max: 0.1f,
                interval: 0.005f
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Change Speed",
                tooltip: () => "How quickly the light adjusts to new values (lower = smoother)",
                getValue: () => Config.RadiusChangeSpeed,
                setValue: value => Config.RadiusChangeSpeed = value,
                min: 0.005f,
                max: 0.5f,
                interval: 0.005f
            );
        }

        public class FlickerData
        {
            public Color BaseColor;
            public float BaseRadius;
            public float CurrentRadius;
            public Color CurrentColor;

            private float targetRadius;
            private Color targetColor;
            private int framesUntilNextBigFlicker;
            private float bigFlickerIntensity;

            public FlickerData(Color baseColor, float baseRadius)
            {
                BaseColor = baseColor;
                BaseRadius = baseRadius;
                CurrentRadius = baseRadius;
                CurrentColor = baseColor;
                targetRadius = GetRandomRadius();
                targetColor = GetRandomColor();
                framesUntilNextBigFlicker = ModEntry.GlobalRandom.Next(60, 180);
            }

            public void Update()
            {
                CurrentRadius = MoveToward(CurrentRadius, targetRadius, ModEntry.Config.RadiusChangeSpeed);
                CurrentColor = MoveToward(CurrentColor, targetColor, ModEntry.Config.RadiusChangeSpeed);

                if (Math.Abs(CurrentRadius - targetRadius) < 0.01f)
                    targetRadius = GetRandomRadius();

                if (ColorDistance(CurrentColor, targetColor) < 0.01f)
                    targetColor = GetRandomColor();

                if (ModEntry.GlobalRandom.NextDouble() < ModEntry.Config.BigFlickerChance)
                {
                    bigFlickerIntensity = ModEntry.Config.MinBigFlickerIntensity +
                        (float)ModEntry.GlobalRandom.NextDouble() *
                        (ModEntry.Config.MaxBigFlickerIntensity - ModEntry.Config.MinBigFlickerIntensity);
                }

                bigFlickerIntensity = Math.Max(0, bigFlickerIntensity - 0.01f);
            }


            private float GetRandomRadius()
            {
                float variation = (float)(ModEntry.GlobalRandom.NextDouble() * 2 - 1) * ModEntry.Config.RadiusJitterAmount;
                if (bigFlickerIntensity > 0)
                    variation += (float)ModEntry.GlobalRandom.NextDouble() * bigFlickerIntensity * 0.5f;

                return BaseRadius * (1 + variation);
            }

            private Color GetRandomColor()
            {
                float r = BaseColor.R / 255f;
                float g = BaseColor.G / 255f;
                float b = BaseColor.B / 255f;

                float variation = (float)(ModEntry.GlobalRandom.NextDouble() * 2 - 1) * ModEntry.Config.ColorJitterAmount;

                if (bigFlickerIntensity > 0)
                {
                    r = Math.Min(1, r + bigFlickerIntensity * 0.1f);
                    g = Math.Min(1, g + bigFlickerIntensity * 0.05f);
                    b = Math.Max(0, b - bigFlickerIntensity * 0.1f);
                }

                r = Math.Clamp(r + variation * 0.5f, 0, 1);
                g = Math.Clamp(g + variation * 0.7f, 0, 1);
                b = Math.Clamp(b + variation * 0.3f, 0, 1);

                return new Color(r, g, b);
            }

            private float MoveToward(float current, float target, float maxDelta)
            {
                if (Math.Abs(target - current) <= maxDelta)
                    return target;
                return current + Math.Sign(target - current) * maxDelta;
            }

            private Color MoveToward(Color current, Color target, float maxDelta)
            {
                return new Color(
                    MoveToward(current.R / 255f, target.R / 255f, maxDelta),
                    MoveToward(current.G / 255f, target.G / 255f, maxDelta),
                    MoveToward(current.B / 255f, target.B / 255f, maxDelta)
                );
            }

            private float ColorDistance(Color a, Color b)
            {
                return Math.Abs(a.R - b.R) / 255f +
                       Math.Abs(a.G - b.G) / 255f +
                       Math.Abs(a.B - b.B) / 255f;
            }

            public Color GetFlickeringColor() => CurrentColor;
            public float GetFlickeringRadius() => CurrentRadius;
        }

        [HarmonyPatch(typeof(LightSource), nameof(LightSource.Draw))]
        public class LightSourceDrawPatch
        {
            public static void Prefix(LightSource __instance)
            {
                try
                {
                    // match any sconce light (so this works for torches, etc.)
                    if (__instance.textureIndex.Value == 4)
                    {
                        string lightId = __instance.Id ?? __instance.position.Value.ToString();

                        if (!ModEntry.FlickeringLights.TryGetValue(lightId, out FlickerData flickerData))
                        {
                            flickerData = new FlickerData(__instance.color.Value, __instance.radius.Value);
                            ModEntry.FlickeringLights[lightId] = flickerData;
                        }

                        __instance.color.Value = flickerData.GetFlickeringColor();
                        __instance.radius.Value = flickerData.GetFlickeringRadius();
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor?.Log($"Error in flicker: {ex}", LogLevel.Error);
                }
            }
        }
    }
}