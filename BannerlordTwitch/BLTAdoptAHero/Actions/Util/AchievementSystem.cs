﻿using System.ComponentModel;
using JetBrains.Annotations;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System;

namespace BLTAdoptAHero.Actions.Util
{
    public class AchievementSystem
    {

        [Description("Unique ID of the achievement, so you can change the Name without clearing player progress"), PropertyOrder(1), ReadOnly(true)]
        public Guid ID { get; set; } = Guid.NewGuid();
        [PropertyOrder(2)]
        public bool Enabled { get; [UsedImplicitly] set; }
        [PropertyOrder(3)]
        public string Name { get; [UsedImplicitly] set; }
        [PropertyOrder(4), Description("Text that will display when the achievement is gained and when the player lists their achievements.")]
        public string NotificationText { get; [UsedImplicitly] set; }

        [PropertyOrder(5), Description("Type of achievement this will be.")]
        public AchievementTypes Type { get; [UsedImplicitly] set; }

        [PropertyOrder(6), Description("Value needed to obtain the achievement.")]
        public int Value { get; [UsedImplicitly] set; }
        [PropertyOrder(7), Description("Gold awarded for gaining this achievement.")]
        public int GoldGain { get; [UsedImplicitly] set; }
        [PropertyOrder(8), Description("Experience awarded for gaining this achievement.")]
        public int XPGain { get; [UsedImplicitly] set; }

        public enum AchievementTypes
        {
            None,
            TotalKills,
            TotalBLTKills,
            TotalMainKills,
            Summons,
            Attacks,
            Deaths
        };

        public override string ToString() => Name;


        
    }
}
