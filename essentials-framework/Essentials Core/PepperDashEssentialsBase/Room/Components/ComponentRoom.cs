﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Interfaces.Components;
using PepperDash.Essentials.Core.Config;

using Newtonsoft.Json;

namespace PepperDash.Essentials.Core.Room
{
    /// <summary>
    /// The base config class for various component types
    /// </summary>
    public abstract class RoomComponentConfig : DeviceConfig
    {

    }

    /// <summary>
    /// The config class for an activiry
    /// </summary>
    public class RoomActivityConfig : RoomComponentConfig
    {
        [JsonProperty("label")]
        public string Label { get; set; }
        [JsonProperty("icon")]
        public string Icon { get; set; }
        [JsonProperty("componentKey")]
        public string ComponentKey { get; set; }
        [JsonProperty("order")]
        public int Order { get; set; }
    }

    /// <summary>
    /// The config class for a room behaviour
    /// </summary>
    public class RoomBehaviourConfig : RoomComponentConfig
    {

    }

    /// <summary>
    /// The config class for a device behavior
    /// </summary>
    public class RoomDeviceBehaviourConfig : RoomComponentConfig
    {

    }

    public class ComponentRoomPropertiesConfig
    {
        [JsonProperty("activities")]
        public List<RoomActivityConfig> Activities { get; set; }
        [JsonProperty("components")]
        public List<RoomComponentConfig> Components { get; set; }

    }

    public class ComponentRoom : Device, IComponentRoom
    {
        public List<IRoomComponent> Components { get; private set; }
        public List<IRoomActivityComponent> Activities { get; private set; }

        public ComponentRoom(string key, string name)
            : base(key, name)
        {

        }

        public List<IRoomComponent> GetRoomComponentsOfType(Type componentType)
        {
            // TODO: Figure this out later
            return Components;
            //var results = Components.OfType<componentType>();
            //return results;
            //return Components.Where(c => c != null && type.IsAssignableFrom(c.GetType()));
        }

        /// <summary>
        /// Returns a list of the activies sorted by order
        /// </summary>
        /// <returns></returns>
        public List<IRoomActivityComponent> GetOrderedActvities()
        {
            return Activities.OrderBy(a => a.Order).ToList<IRoomActivityComponent>();
        }

    }
}