﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Powers
{
    [Description("Adds fixed or relative amount of extra HP to the hero when they spawn"), UsedImplicitly]
    public class AddHealthPower : HeroPowerDefBase, IHeroPowerPassive
    {
        [Category("Power Config"), Description("How much to multiply base HP by"), PropertyOrder(1), UsedImplicitly]
        public float HealthToMultiply { get; set; } = 1f;

        [Category("Power Config"), Description("How much HP to add"), PropertyOrder(2), UsedImplicitly]
        public float HealthToAdd { get; set; }

        public AddHealthPower()
        {
            Type = new ("C4213666-2176-42B4-8DBB-BFE0182BCCE1");
        }

        public void OnAdded(Hero hero) {}
        public void OnRemoved(Hero hero) {}
        public void OnBattleStart(Hero hero) {}
        public void OnBattleTick(Hero hero, Agent agent) {}
        public void OnBattleEnd(Hero hero) {}

        public void OnAgentBuild(Hero hero, Agent agent)
        {
            agent.BaseHealthLimit *= HealthToMultiply;
            agent.HealthLimit *= HealthToMultiply;
            agent.Health *= HealthToMultiply;

            agent.BaseHealthLimit += HealthToAdd;
            agent.HealthLimit += HealthToAdd;
            agent.Health += HealthToAdd;
        }
        
        public void OnAgentKilled(Hero hero, Agent agent, Hero killerHero, Agent killerAgent) {}
        public void OnDoDamage(Hero hero, Agent agent, Hero victimHero, Agent victimAgent, ref AttackCollisionData attackCollisionData) {}
        public void OnTakeDamage(Hero hero, Agent agent, Hero attackerHero, Agent attackerAgent, ref AttackCollisionData attackCollisionData) {}
        
        public override string ToString()
        {
            var parts = new List<string>();
            if (HealthToMultiply != 1)
            {
                parts.Add($"x{HealthToMultiply:0.0} ({HealthToMultiply * 100:0.0}%) health");
            }
            if (HealthToAdd != 0)
            {
                parts.Add(HealthToAdd > 0 ? $"+{HealthToAdd:0.0} health" : $"{HealthToAdd:0.0} health");
            }
            return $"{Name}: {string.Join(", ", parts)}";
        }
    }
}