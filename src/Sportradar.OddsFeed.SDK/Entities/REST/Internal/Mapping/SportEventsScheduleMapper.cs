﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System.Linq;
using Dawn;
using Sportradar.OddsFeed.SDK.Entities.Rest.Internal.Dto;
using Sportradar.OddsFeed.SDK.Messages.Rest;

namespace Sportradar.OddsFeed.SDK.Entities.Rest.Internal.Mapping
{
    /// <summary>
    /// Maps <see cref="scheduleEndpoint" /> instances to list of <see cref="SportEventSummaryDto" /> instances
    /// </summary>
    internal class SportEventsScheduleMapper : ISingleTypeMapper<EntityList<SportEventSummaryDto>>
    {
        /// <summary>
        /// A <see cref="scheduleEndpoint"/> instance containing schedule for a day
        /// </summary>
        private readonly scheduleEndpoint _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="SportEventsScheduleMapper"/> class.
        /// </summary>
        /// <param name="data">A <see cref="scheduleEndpoint"/> instance containing schedule information</param>
        internal SportEventsScheduleMapper(scheduleEndpoint data)
        {
            Guard.Argument(data, nameof(data)).NotNull();

            _data = data;
        }

        /// <summary>
        /// Maps it's data to <see cref="EntityList{SportEventSummaryDto}"/> instance
        /// </summary>
        /// <returns>The created <see cref="EntityList{SportEventSummaryDto}"/> instance</returns>
        public EntityList<SportEventSummaryDto> Map()
        {
            var events = _data.sport_event.Select(e => RestMapperHelper.MapSportEvent(e)).ToList();
            return new EntityList<SportEventSummaryDto>(events);
        }
    }
}
