﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrameWork;
using NLog;

namespace WorldServer.World.Battlefronts.Apocalypse.Loot
{
    public class RewardAssigner : IRewardAssigner
    {
        public IRandomGenerator RandomGenerator { get; set; }
        public IRewardSelector RewardSelector { get; }

        private static readonly Logger RewardLogger = LogManager.GetLogger("RewardLogger");
        public RewardAssigner(IRandomGenerator randomGenerator, IRewardSelector rewardSelector)
        {
            RandomGenerator = randomGenerator;
            RewardSelector = rewardSelector;
        }

        /// <summary>
        /// For a list of player Ids, select those getting a reward, and assign a colored loot bag (reward) to them.
        /// </summary>
        /// <param name="eligiblePlayers"></param>
        /// <param name="forceNumberRewards"></param>
        /// <returns></returns>
        public List<LootBagTypeDefinition> AssignLootToPlayers(List<uint> eligiblePlayers, int forceNumberRewards = 0)
        {
            if (eligiblePlayers.Count == 0)
                return null;

            // Randomise the players
            // var randomisedPlayerList = eligiblePlayers.OrderBy(a => Guid.NewGuid()).ToList();
            //var randomisedPlayerList = RewardSelector.RandomisePlayerList(eligiblePlayers);
            int numberLootBags = 0;
            // Determine the number of awards to give
            numberLootBags = forceNumberRewards == 0 ? RewardSelector.DetermineNumberOfAwards((uint)eligiblePlayers.Count()) : forceNumberRewards;

            // Define the types of awards to give
            var lootBagDefinitions = new LootBagTypeDefinition().BuildLootBagTypeDefinitions(numberLootBags);
            RewardLogger.Debug($"Number loot bags {lootBagDefinitions.Count}");
            foreach (var lootBagTypeDefinition in lootBagDefinitions)
            {
                try
                {
                    var selectedPlayer = eligiblePlayers[StaticRandom.Instance.Next((eligiblePlayers.Count))];

                    RewardLogger.Debug($"Selected player {selectedPlayer} {eligiblePlayers.Count} for reward");

                    lootBagTypeDefinition.Assignee = selectedPlayer;
                }
                catch (Exception e)
                {
                    RewardLogger.Warn($"{e.Message}");
                }
            }
            return lootBagDefinitions;
        }
    }
}