﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Behaviors;
using HarmonyLib;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Behaviour that is active for all missions
    /// </summary>
    [HarmonyPatch]
    internal class BLTAdoptAHeroCommonMissionBehavior : AutoMissionBehavior<BLTAdoptAHeroCommonMissionBehavior>
    {
        private MissionInfoPanel missionInfoPanel;

        private ObservableCollection<HeroViewModel> heroesViewModel { get; set; } = new();
        private CollectionViewSource heroesSortedView { get; set; }
        private readonly List<Hero> activeHeroes = new();

        private class HeroMissionState
        {
            public int WonGold { get; set; }
            public int WonXP { get; set; }
            public int Kills { get; set; }
            public int RetinueKills { get; set; }
            public int KillStreak { get; set; }
        }

        private Dictionary<Hero, HeroMissionState> heroMissionState = new();
        private readonly List<Agent> adoptedHeroMounts = new();

        public float PlayerSidePower { get; private set; }
        public float EnemySidePower { get; private set; }
        public float PlayerPowerRatio => PlayerSidePower / Math.Max(1, EnemySidePower);
        public float EnemyPowerRatio => EnemySidePower / Math.Max(1, PlayerSidePower);

        public float PlayerSideRewardMultiplier
        {
            get
            {
                if (BLTAdoptAHeroModule.CommonConfig.DifficultyScalingOnPlayersSide)
                {
                    return MathF.Clamp(MathF.Pow(EnemyPowerRatio, BLTAdoptAHeroModule.CommonConfig.DifficultyScalingClamped),
                        BLTAdoptAHeroModule.CommonConfig.DifficultyScalingMinClamped, 
                        BLTAdoptAHeroModule.CommonConfig.DifficultyScalingMaxClamped);
                }
                else
                {
                    return 1;
                }
            }
        }
        
        public float EnemySideRewardMultiplier
        {
            get
            {
                if (BLTAdoptAHeroModule.CommonConfig.DifficultyScalingOnEnemySide)
                {
                    return MathF.Clamp(MathF.Pow(PlayerPowerRatio, BLTAdoptAHeroModule.CommonConfig.DifficultyScalingClamped),
                        BLTAdoptAHeroModule.CommonConfig.DifficultyScalingMinClamped, 
                        BLTAdoptAHeroModule.CommonConfig.DifficultyScalingMaxClamped);
                }
                else
                {
                    return 1;
                }
            }
        }

        public BLTAdoptAHeroCommonMissionBehavior()
        {
            Log.AddInfoPanel(() =>
            {
                heroesSortedView = new CollectionViewSource { Source = heroesViewModel };
                heroesSortedView.SortDescriptions.Add(new SortDescription(nameof(HeroViewModel.GlobalSortKey), ListSortDirection.Descending));
                heroesSortedView.IsLiveSortingRequested = true;
                heroesSortedView.LiveSortingProperties.Add(nameof(HeroViewModel.GlobalSortKey));
                missionInfoPanel = new MissionInfoPanel {HeroList = { ItemsSource = heroesSortedView.View }};
                return missionInfoPanel;
            });
        }
        
        public override void OnAgentCreated(Agent agent)
        {
            try
            {
                var hero = agent.GetAdoptedHero();
                if (hero == null)
                {
                    return;
                }

                BLTAdoptAHeroCampaignBehavior.SetAgentStartingHealth(hero, agent);
                activeHeroes.Add(hero);
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnAgentCreated)}", ex);
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                var hero = agent.GetAdoptedHero();
                if (hero != null && agent.MountAgent != null)
                {
                    adoptedHeroMounts.Add(agent.MountAgent);
                }

                if (!agent.IsMount && agent.Team?.IsValid == true && Mission.PlayerTeam?.IsValid == true)
                {
                    if (agent.Team.IsFriendOf(Mission.PlayerTeam))
                    {
                        PlayerSidePower += agent.Character.GetPower();
                    }
                    else
                    {
                        EnemySidePower += agent.Character.GetPower();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnAgentBuild)}", ex);
            }
        }

        private float slowTickT;
        private float fasterTickT;

        public override void OnMissionTick(float dt)
        {
            try
            {
                slowTickT += dt;
                const float TickTime = 2f;
                if (slowTickT > TickTime)
                {
                    slowTickT -= TickTime;

                    foreach (var h in activeHeroes)
                    {
                        UpdateHeroVM(h);
                    }
                }

                fasterTickT += dt;
                const float FasterTickTime = 0.05f;
                if (fasterTickT > FasterTickTime)
                {
                    fasterTickT -= FasterTickTime;

                    foreach (var h in activeHeroes)
                    {
                        UpdateHeroVMTick(h);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnMissionTick)}", ex);
            }
        }

        protected override void OnEndMission()
        {
            try
            {
                Log.RemoveInfoPanel(missionInfoPanel);
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnEndMission)}", ex);
            }
        }

        // public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, int damage, in MissionWeapon affectorWeapon)
        // {
        //     UpdateHeroVM(affectedAgent);
        // }

        [UsedImplicitly, HarmonyPrefix, HarmonyPatch(typeof(Mission), "OnAgentRemoved")]
        public static void OnAgentRemovedPrefix(Mission __instance, Agent affectedAgent, Agent affectorAgent,
            ref AgentState agentState, KillingBlow killingBlow)
        {
            try
            {
                if (affectedAgent.State == AgentState.Killed)
                {
                    // Stop hero from dying if death is disabled
                    var affectedHero = affectedAgent.GetAdoptedHero();
                    if (affectedHero != null 
                        && (BLTAdoptAHeroModule.CommonConfig.AllowDeath == false
                            || StaticRandom.Next() > BLTAdoptAHeroModule.CommonConfig.DeathChance))
                    {
                        agentState = affectedAgent.State = AgentState.Unconscious;
                    }

                    // Stop horse from dying always, as its not easily replaceable
                    if (affectedAgent.IsMount && Current?.adoptedHeroMounts.Contains(affectedAgent) == true)
                    {
                        agentState = affectedAgent.State = AgentState.Unconscious;
                    }
                }

                // Remove mount agent from tracking, in-case it is reused
                Current?.adoptedHeroMounts.Remove(affectedAgent);
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnAgentRemovedPrefix)}", ex);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                var affectedHero = affectedAgent.GetAdoptedHero();
                if (affectedHero != null)
                {
                    Log.Trace($"[{nameof(BLTAdoptAHeroCommonMissionBehavior)}] {affectedHero} was made {agentState} by {affectorAgent?.Name ?? "unknown"}");
                    ApplyKilledEffects(
                        affectedHero, affectorAgent, agentState,
                        BLTAdoptAHeroModule.CommonConfig.XPPerKilled,
                        Math.Max(BLTAdoptAHeroModule.CommonConfig.SubBoost, 1),
                        BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                        BLTAdoptAHeroModule.CommonConfig.LevelScalingCap
                    );
                    ResetKillStreak(affectedHero);
                    BLTAdoptAHeroCampaignBehavior.Current.IncreaseHeroDeaths(affectedHero);
                }

                var affectorHero = affectorAgent.GetAdoptedHero();
                if (affectorHero != null)
                {
                    float horseFactor = affectedAgent?.IsHuman == false ? 0.25f : 1;
                    Log.Trace($"[{nameof(BLTAdoptAHeroCommonMissionBehavior)}] {affectorHero} made {affectedAgent?.Name ?? "unknown"} {agentState}");
                    ApplyKillEffects(
                        affectorHero, affectorAgent, affectedAgent, agentState,
                        (int) (BLTAdoptAHeroModule.CommonConfig.GoldPerKill * horseFactor),
                        (int) (BLTAdoptAHeroModule.CommonConfig.HealPerKill * horseFactor),
                        (int) (BLTAdoptAHeroModule.CommonConfig.XPPerKill * horseFactor),
                        Math.Max(BLTAdoptAHeroModule.CommonConfig.SubBoost, 1),
                        BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                        BLTAdoptAHeroModule.CommonConfig.LevelScalingCap
                    );

                    //UpdateHeroVM(affectorAgent);
                    if (affectedAgent?.IsHuman == true && agentState is AgentState.Unconscious or AgentState.Killed)
                    {
                        GetHeroMissionState(affectorHero).Kills++;
                        AddKillStreak(affectorHero);
                        BLTAdoptAHeroCampaignBehavior.Current.IncreaseHeroKills(affectorHero, affectedAgent);
                    }
                }

                var affectorRetinueOwner = BLTSummonBehavior.Current?.GetSummonedHeroForRetinue(affectorAgent);
                if (affectorRetinueOwner != null)
                {
                    GetHeroMissionState(affectorRetinueOwner.Hero).RetinueKills++;
                }
                
            }
            catch (Exception ex)
            {
                Log.Exception($"{nameof(BLTAdoptAHeroCommonMissionBehavior)}.{nameof(OnAgentRemoved)}", ex);
            }
        }

        // public override void OnAgentFleeing(Agent affectedAgent)
        // {
        //     
        // }
        //
        // public override void OnAgentPanicked(Agent affectedAgent)
        // {
        //     
        // }
        
        private void AddKillStreak(Hero hero)
        {
            // declare variable right where it's passed
            var heroState = GetHeroMissionState(hero);
            heroState.KillStreak++;

            var currKillStreak = BLTAdoptAHeroModule.CommonConfig.KillStreaks?.FirstOrDefault(k => k.Enabled && heroState.KillStreak == k.KillsRequired);
            if (currKillStreak != null)
            {
                string message = currKillStreak.NotificationText
                    .Replace("{player}", hero.FirstName.ToString())
                    .Replace("{kills}",currKillStreak.KillsRequired.ToString())
                    .Replace("{name}",currKillStreak.Name);
                if (BLTAdoptAHeroModule.CommonConfig.ShowKillStreakPopup && currKillStreak.ShowNotification)
                {
                    Log.ShowInformation(message, hero.CharacterObject, BLTAdoptAHeroModule.CommonConfig.KillStreakPopupAlertSound);
                }
                ApplyStreakEffects(hero, currKillStreak.GoldReward, currKillStreak.XPReward,Math.Max(BLTAdoptAHeroModule.CommonConfig.SubBoost, 1),currKillStreak.Name,BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,BLTAdoptAHeroModule.CommonConfig.LevelScalingCap, message);
            }
        }

        private void ResetKillStreak(Hero hero)
        {
            GetHeroMissionState(hero).KillStreak = 0;
        }

        private HeroMissionState GetHeroMissionState(Hero hero)
        {
            if (!heroMissionState.TryGetValue(hero, out var state))
            {
                state = new HeroMissionState();
                heroMissionState.Add(hero, state);
            }

            return state;
        }

        // private void UpdateHeroVM(Agent agent)
        // {
        //     var hero = agent.GetAdoptedHero();
        //     if (hero != null)
        //     {
        //         UpdateHeroVM(hero, agent);
        //     }
        // }

        private void UpdateHeroVMTick(Hero hero)
        {
            if (!activeHeroes.Contains(hero))
            {
                activeHeroes.Add(hero);
            }

            var summonState = BLTSummonBehavior.Current?.GetSummonedHero(hero);

            var agent = summonState?.CurrentAgent ??
                        Mission.Current.Agents.FirstOrDefault(a => a.Character == hero.CharacterObject);

            string name = hero.FirstName.Raw();
            float HP = agent?.Health ?? 0;
            float cooldownFractionRemaining = 1 - summonState?.CoolDownFraction ?? 0;
            float cooldownSecondsRemaining = summonState?.CooldownRemaining ?? 0;
            
            Log.RunInfoPanelUpdate(() =>
            {
                var hm = heroesViewModel.FirstOrDefault(h => h.Name == name);
                if (hm != null)
                {
                    hm.HP = HP;
                    hm.CooldownFractionRemaining = cooldownFractionRemaining;
                    hm.CooldownSecondsRemaining = cooldownSecondsRemaining;
                }
            });
        }

        private static bool IsHeroOnPlayerSide(Hero hero) => hero.PartyBelongedTo?.MapEventSide?.MissionSide == PlayerEncounter.Current?.PlayerSide;

        private void UpdateHeroVM(Hero hero)
        {
            var heroState = GetHeroMissionState(hero);

            if (!activeHeroes.Contains(hero))
            {
                activeHeroes.Add(hero);
            }

            var summonState = BLTSummonBehavior.Current?.GetSummonedHero(hero);

            var agent = summonState?.CurrentAgent ?? hero.GetAgent();

            var state = summonState?.State ?? agent?.State ?? AgentState.None;
            var heroModel = new HeroViewModel
            {
                Name = hero.FirstName.Raw(),
                IsPlayerSide = summonState?.WasPlayerSide ?? IsHeroOnPlayerSide(hero),
                MaxHP = agent?.HealthLimit ?? 100,
                HP = agent?.Health ?? 0,
                IsRouted = state is AgentState.Routed,
                IsUnconscious = state is AgentState.Unconscious,
                IsKilled = state is AgentState.Killed,
                Retinue = summonState?.ActiveRetinue ?? 0,
                GoldEarned = heroState.WonGold,
                XPEarned = heroState.WonXP,
                CooldownFractionRemaining = 1 - summonState?.CoolDownFraction ?? 0,
                CooldownSecondsRemaining = summonState?.CooldownRemaining ?? 0,
                Kills = heroState.Kills,
                RetinueKills = heroState.RetinueKills,
            };
            
            bool shouldRemove = agent?.State is not AgentState.Active && MissionHelpers.InTournament();
            Log.RunInfoPanelUpdate(() =>
            {
                var hm = heroesViewModel.FirstOrDefault(h => h.Name == heroModel.Name);
                if (shouldRemove)
                {
                    if (hm != null)
                    {
                        heroesViewModel.Remove(hm);
                    }
                }
                else if (hm != null)
                {
                    hm.Name = heroModel.Name;
                    hm.IsPlayerSide = heroModel.IsPlayerSide;
                    hm.MaxHP = heroModel.MaxHP;
                    hm.HP = heroModel.HP;
                    hm.IsRouted = heroModel.IsRouted;
                    hm.IsUnconscious = heroModel.IsUnconscious;
                    hm.IsKilled = heroModel.IsKilled;
                    hm.Retinue = heroModel.Retinue;
                    hm.GoldEarned = heroModel.GoldEarned;
                    hm.XPEarned = heroModel.XPEarned;
                    hm.Kills = heroModel.Kills;
                    hm.RetinueKills = heroModel.RetinueKills;
                    hm.CooldownFractionRemaining = heroModel.CooldownFractionRemaining;
                    hm.CooldownSecondsRemaining = heroModel.CooldownSecondsRemaining;
                }
                else
                {
                    heroesViewModel.Add(heroModel);
                }
            });
        }
        // public static string KillStateVerb(AgentState state) =>
        //     state switch
        //     {
        //         AgentState.Routed => "routed",
        //         AgentState.Unconscious => "knocked out",
        //         AgentState.Killed => "killed",
        //         AgentState.Deleted => "deleted",
        //         _ => "fondled"
        //     };
        
        // public const int MaxLevel = 62;
        public const int MaxLevelInPractice = 32;
        
        // https://www.desmos.com/calculator/frzo6bkrwv
        // value returned is 0 < v < 1 if levelB < levelA, v = 1 if they are equal, and 1 < v < max if levelB > levelA
        public static float RelativeLevelScaling(int levelA, int levelB, float n, float max = float.MaxValue) 
            => Math.Min(MathF.Pow(1f - Math.Min(MaxLevelInPractice - 1, levelB - levelA) / (float)MaxLevelInPractice, -10f * MathF.Clamp(n, 0, 1)), max);
        
        public void ApplyStreakEffects(Hero hero, int goldStreak, int xpStreak, float subBoost, string killStreakName, float? relativeLevelScaling, float? levelScalingCap, string message)
        {
            goldStreak = (int)(goldStreak * subBoost);
            xpStreak = (int)(xpStreak * subBoost);

            if (relativeLevelScaling.HasValue)
            {
                // More reward for killing higher level characters
                float levelBoost = RelativeLevelScaling(hero.Level, BLTAdoptAHeroModule.CommonConfig.ReferenceLevelReward, relativeLevelScaling.Value, levelScalingCap ?? 5);

                goldStreak = (int)(goldStreak * levelBoost);
                xpStreak = (int)(xpStreak * levelBoost);
            }

            if (goldStreak != 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, goldStreak);
                GetHeroMissionState(hero).WonGold += goldStreak;
            }

            if (xpStreak != 0)
            {
                (bool success, string _) = SkillXP.ImproveSkill(hero, xpStreak, SkillsEnum.All, auto: true);
                if (success)
                {
                    GetHeroMissionState(hero).WonXP += xpStreak;
                }
            }
        }

        public void ApplyKillEffects(Hero hero, Agent killer, Agent killed, AgentState state, int goldPerKill, int healPerKill, int xpPerKill, float subBoost, float? relativeLevelScaling, float? levelScalingCap)
        {
            goldPerKill = (int) (goldPerKill * subBoost);
            healPerKill = (int) (healPerKill * subBoost);
            xpPerKill = (int) (xpPerKill * subBoost);

            if (relativeLevelScaling.HasValue && killed?.Character != null)
            {
                // More reward for killing higher level characters
                float levelBoost = RelativeLevelScaling(hero.Level, killed.Character.Level, relativeLevelScaling.Value, levelScalingCap ?? 5);

                goldPerKill = (int) (goldPerKill * levelBoost);
                healPerKill = (int) (healPerKill * levelBoost);
                xpPerKill = (int) (xpPerKill * levelBoost);
            }

            if (goldPerKill != 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, goldPerKill);
                GetHeroMissionState(hero).WonGold += goldPerKill;
            }
            
            if (healPerKill != 0)
            {
                killer.Health = Math.Min(killer.HealthLimit, killer.Health + healPerKill);
            }

            if (xpPerKill != 0)
            {
                SkillXP.ImproveSkill(hero, xpPerKill, SkillsEnum.All, auto: true);
                GetHeroMissionState(hero).WonXP += xpPerKill;
            }
        }
        
        public void ApplyKilledEffects(Hero hero, Agent killer, AgentState state, int xpPerKilled, float subBoost, float? relativeLevelScaling, float? levelScalingCap)
        {
            xpPerKilled = (int) (xpPerKilled * subBoost);

            if (relativeLevelScaling.HasValue && killer?.Character != null)
            {
                // More reward for being killed by higher level characters
                float levelBoost = RelativeLevelScaling(hero.Level, killer.Character.Level, relativeLevelScaling.Value, levelScalingCap ?? 5);

                xpPerKilled = (int) (xpPerKilled * levelBoost);
            }

            if (xpPerKilled != 0)
            {
                SkillXP.ImproveSkill(hero, xpPerKilled, SkillsEnum.All, auto: true);
                GetHeroMissionState(hero).WonXP += xpPerKilled;
            }
        }

        public void ApplyAchievementRewards(Hero hero, int goldGained, int xpGained)
        {
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, goldGained);
            GetHeroMissionState(hero).WonGold += goldGained;

            SkillXP.ImproveSkill(hero, xpGained, SkillsEnum.All, auto: true);
            GetHeroMissionState(hero).WonXP += xpGained;
        }
    }
}