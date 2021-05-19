﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Rewards;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    internal abstract class ImproveAdoptedHero : ActionHandlerBase
    {
        protected class SettingsBase
        {
            [Description("Lower bound of amount to improve"), PropertyOrder(11)]
            public int AmountLow { get; set; }
            [Description("Upper bound of amount to improve"), PropertyOrder(12)]
            public int AmountHigh { get; set; }
            [Description("Gold that will be taken from the hero"), PropertyOrder(13)]
            public int GoldCost { get; set; }
        }

        // protected override Type ConfigType => typeof(SettingsBase);

        protected override void ExecuteInternal(ReplyContext context, object config,
            Action<string> onSuccess,
            Action<string> onFailure) 
        {
            var settings = (SettingsBase)config;
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(Campaign.Current == null ? AdoptAHero.NotStartedMessage : AdoptAHero.NoHeroMessage);
                return;
            }

            int availableGold = BLTAdoptAHeroCampaignBehavior.Get().GetHeroGold(adoptedHero);
            if (availableGold < settings.GoldCost)
            {
                onFailure($"You do not have enough gold: you need {settings.GoldCost}, and you only have {availableGold}!");
                return;
            }
            
            int amount = MBRandom.RandomInt(settings.AmountLow, settings.AmountHigh);
            (bool success, string description) = Improve(context.UserName, adoptedHero, amount, settings);
            if (success)
            {
                onSuccess(description);
                BLTAdoptAHeroCampaignBehavior.Get().ChangeHeroGold(adoptedHero, -settings.GoldCost);
            }
            else
            {
                onFailure(description);
            }
        }

        protected abstract (bool success, string description) Improve(string userName, Hero adoptedHero, int amount, SettingsBase settings);

        private static string[] EnumFlagsToArray<T>(T flags) =>
            flags.ToString().Split(',').Select(s => s.Trim()).ToArray();
        private static (string[] skillNames, float weight)[] SkillGroups =
        {
            (skillNames: SkillGroup.SkillsToStrings(Skills.Melee), weight: 3f),
            (skillNames: SkillGroup.SkillsToStrings(Skills.Ranged), weight: 2f),
            (skillNames: SkillGroup.SkillsToStrings(Skills.Support), weight: 1f),
            (skillNames: SkillGroup.SkillsToStrings(Skills.Movement), weight: 2f),
            (skillNames: SkillGroup.SkillsToStrings(Skills.Personal), weight: 1f),
        };
        
        protected static SkillObject GetSkill(Hero hero, Skills skills, bool random, bool auto, Func<SkillObject, bool> predicate = null)
        {
            predicate ??= s => true;
            IEnumerable<SkillObject> selectedSkills;
            if (auto)
            {
                // We will select automatically which skill from groups
                selectedSkills = SkillGroups
                    .Select(g => (skillNames: SkillGroup.GetSkills(g.skillNames).Where(predicate), weight: g.weight))
                    .Where(g => g.skillNames.Any())
                    .SelectWeighted(MBRandom.RandomFloat, skillsW => skillsW.weight)
                    .skillNames;
            }
            else
            {
                selectedSkills = SkillGroup.GetSkills(SkillGroup.SkillsToStrings(skills)).Where(predicate);
            }

            return random 
                ? selectedSkills?.SelectRandom() 
                : selectedSkills?.SelectWeighted(MBRandom.RandomFloat, o => hero.GetSkillValue(o) + 1);
        }
    }
}