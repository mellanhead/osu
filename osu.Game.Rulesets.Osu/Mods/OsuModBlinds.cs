﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModBlinds : Mod, IApplicableToRulesetContainer<OsuHitObject>, IApplicableToScoreProcessor
    {
        public override string Name => "Blinds";
        public override string Description => "Play with blinds on your screen.";
        public override string Acronym => "BL";

        public override FontAwesome Icon => FontAwesome.fa_adjust;
        public override ModType Type => ModType.DifficultyIncrease;

        public override bool Ranked => false;

        public override double ScoreMultiplier => 1.12;
        private DrawableOsuBlinds blinds;

        public void ApplyToRulesetContainer(RulesetContainer<OsuHitObject> rulesetContainer)
        {
            bool hasEasy = rulesetContainer.Mods.Any(m => m is ModEasy);
            bool hasHardrock = rulesetContainer.Mods.Any(m => m is ModHardRock);

            rulesetContainer.Overlays.Add(blinds = new DrawableOsuBlinds(rulesetContainer.Playfield.HitObjectContainer, hasEasy, hasHardrock, rulesetContainer.Beatmap));
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            scoreProcessor.Health.ValueChanged += val => { blinds.AnimateClosedness((float)val); };
        }

        /// <summary>
        /// Element for the Blinds mod drawing 2 black boxes covering the whole screen which resize inside a restricted area with some leniency.
        /// </summary>
        public class DrawableOsuBlinds : Container
        {
            /// <summary>
            /// Black background boxes behind blind panel textures.
            /// </summary>
            private Box blackBoxLeft, blackBoxRight;

            private Drawable panelLeft, panelRight, bgPanelLeft, bgPanelRight;

            private readonly Beatmap<OsuHitObject> beatmap;

            /// <summary>
            /// Value between 0 and 1 setting a maximum "closedness" for the blinds.
            /// Useful for animating how far the blinds can be opened while keeping them at the original position if they are wider open than this.
            /// </summary>
            private const float target_clamp = 1;

            private readonly float targetBreakMultiplier = 0;
            private readonly float easing = 1;

            private const float black_depth = 10;
            private const float bg_panel_depth = 8;
            private const float fg_panel_depth = 4;

            private readonly CompositeDrawable restrictTo;
            private readonly bool modEasy, modHardrock;

            /// <summary>
            /// <para>
            /// Percentage of playfield to extend blinds over. Basically moves the origin points where the blinds start.
            /// </para>
            /// <para>
            /// -1 would mean the blinds always cover the whole screen no matter health.
            /// 0 would mean the blinds will only ever be on the edge of the playfield on 0% health.
            /// 1 would mean the blinds are fully outside the playfield on 50% health.
            /// Infinity would mean the blinds are always outside the playfield except on 100% health.
            /// </para>
            /// </summary>
            private const float leniency = 0.1f;

            public DrawableOsuBlinds(CompositeDrawable restrictTo, bool hasEasy, bool hasHardrock, Beatmap<OsuHitObject> beatmap)
            {
                this.restrictTo = restrictTo;
                this.beatmap = beatmap;

                modEasy = hasEasy;
                modHardrock = hasHardrock;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;

                Children = new[]
                {
                    blackBoxLeft = new Box
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Colour = Color4.Black,
                        RelativeSizeAxes = Axes.Y,
                        Width = 0,
                        Height = 1,
                        Depth = black_depth
                    },
                    blackBoxRight = new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Colour = Color4.Black,
                        RelativeSizeAxes = Axes.Y,
                        Width = 0,
                        Height = 1,
                        Depth = black_depth
                    },
                    bgPanelLeft = new ModBlindsPanel
                    {
                        Origin = Anchor.TopRight,
                        Colour = Color4.Gray,
                        Depth = bg_panel_depth + 1
                    },
                    panelLeft = new ModBlindsPanel
                    {
                        Origin = Anchor.TopRight,
                        Depth = bg_panel_depth
                    },
                    bgPanelRight = new ModBlindsPanel
                    {
                        Origin = Anchor.TopLeft,
                        Colour = Color4.Gray,
                        Depth = fg_panel_depth + 1
                    },
                    panelRight = new ModBlindsPanel
                    {
                        Origin = Anchor.TopLeft,
                        Depth = fg_panel_depth
                    },
                };
            }

            private float applyGap(float value)
            {
                const float easy_multiplier = 0.95f;
                const float hardrock_multiplier = 1.1f;

                float multiplier = 1;
                if (modEasy)
                {
                    multiplier = easy_multiplier;
                    // TODO: include OD/CS
                }
                else if (modHardrock)
                {
                    multiplier = hardrock_multiplier;
                    // TODO: include OD/CS
                }

                return MathHelper.Clamp(value * multiplier, 0, target_clamp) * targetBreakMultiplier;
            }

            private static float applyAdjustmentCurve(float value)
            {
                // lagrange polinominal for (0,0) (0.5,0.35) (1,1) should make a good curve
                return 0.6f * value * value + 0.4f * value;
            }

            protected override void Update()
            {
                float start = Parent.ToLocalSpace(restrictTo.ScreenSpaceDrawQuad.TopLeft).X;
                float end = Parent.ToLocalSpace(restrictTo.ScreenSpaceDrawQuad.TopRight).X;

                float rawWidth = end - start;

                start -= rawWidth * leniency * 0.5f;
                end += rawWidth * leniency * 0.5f;

                float width = (end - start) * 0.5f * applyAdjustmentCurve(applyGap(easing));

                // different values in case the playfield ever moves from center to somewhere else.
                blackBoxLeft.Width = start + width;
                blackBoxRight.Width = DrawWidth - end + width;

                panelLeft.X = start + width;
                panelRight.X = end - width;
                bgPanelLeft.X = start;
                bgPanelRight.X = end;
            }

            protected override void LoadComplete()
            {
                const float break_open_early = 500;
                const float break_close_late = 250;

                base.LoadComplete();

                var firstObj = beatmap.HitObjects[0];
                var startDelay = firstObj.StartTime - firstObj.TimePreempt;

                using (BeginAbsoluteSequence(startDelay + break_close_late, true))
                    leaveBreak();

                foreach (var breakInfo in beatmap.Breaks)
                {
                    if (breakInfo.HasEffect)
                    {
                        using (BeginAbsoluteSequence(breakInfo.StartTime - break_open_early, true))
                        {
                            enterBreak();
                            using (BeginDelayedSequence(breakInfo.Duration + break_open_early + break_close_late, true))
                                leaveBreak();
                        }
                    }
                }
            }

            private void enterBreak() => this.TransformTo(nameof(targetBreakMultiplier), 0f, 1000, Easing.OutSine);

            private void leaveBreak() => this.TransformTo(nameof(targetBreakMultiplier), 1f, 2500, Easing.OutBounce);

            /// <summary>
            /// 0 is open, 1 is closed.
            /// </summary>
            public void AnimateClosedness(float value) => this.TransformTo(nameof(easing), value, 200, Easing.OutQuint);

            public class ModBlindsPanel : Sprite
            {
                [BackgroundDependencyLoader]
                private void load(TextureStore textures)
                {
                    Texture = textures.Get("Play/osu/blinds-panel");
                }
            }
        }
    }
}
