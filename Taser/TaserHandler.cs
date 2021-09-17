// -----------------------------------------------------------------------
// <copyright file="TaserHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.CustomItems.API.Features;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Pickups;
using Mistaken.API;
using Mistaken.API.Diagnostics;
using Mistaken.API.Extensions;
using UnityEngine;

namespace Mistaken.Taser
{
    /// <inheritdoc/>
    public class TaserHandler : Module
    {
        /// <inheritdoc cref="Module.Module(Exiled.API.Interfaces.IPlugin{Exiled.API.Interfaces.IConfig})"/>
        public TaserHandler(PluginHandler p)
            : base(p)
        {
            Instance = this;
            new TaserItem().TryRegister();
        }

        /// <inheritdoc/>
        public override string Name => "TaserHandler";

        /// <inheritdoc/>
        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.RoundStarted += this.Handle(() => this.Server_RoundStarted(), "RoundStart");
            Exiled.Events.Handlers.Player.ChangingRole += this.Handle<Exiled.Events.EventArgs.ChangingRoleEventArgs>((ev) => this.Player_ChangingRole(ev));
        }

        /// <inheritdoc/>
        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= this.Handle(() => this.Server_RoundStarted(), "RoundStart");
            Exiled.Events.Handlers.Player.ChangingRole -= this.Handle<Exiled.Events.EventArgs.ChangingRoleEventArgs>((ev) => this.Player_ChangingRole(ev));
        }

        internal static readonly Dictionary<GameObject, Door> Doors = new Dictionary<GameObject, Door>();

        internal static readonly HashSet<ItemType> UsableItems = new HashSet<ItemType>()
        {
            ItemType.MicroHID,
            ItemType.Medkit,
            ItemType.Painkillers,
            ItemType.SCP018,
            ItemType.SCP207,
            ItemType.SCP268,
            ItemType.SCP500,
            ItemType.GrenadeHE,
            ItemType.GrenadeFlash,
            ItemType.Adrenaline,
        };

        internal static TaserHandler Instance { get; set; }

        private void Server_RoundStarted()
        {
            foreach (var door in Map.Doors)
            {
                Doors[door.Base.gameObject] = door;
            }

            var structureLockers = UnityEngine.Object.FindObjectsOfType<MapGeneration.Distributors.SpawnableStructure>().Where(x => x.StructureType == MapGeneration.Distributors.StructureType.LargeGunLocker);
            var lockers = structureLockers.Select(x => x as MapGeneration.Distributors.Locker).Where(x => x.Chambers.Length > 8).ToArray();
            var locker = lockers[UnityEngine.Random.Range(0, lockers.Length)];
            int toSpawn = 1;
            while (toSpawn > 0)
            {
                var chamber = locker.Chambers[UnityEngine.Random.Range(0, locker.Chambers.Length)];
                CustomWeapon.TrySpawn(1, chamber._spawnpoint.position + (Vector3.up / 10), out Pickup pickup);
                chamber._content.Add(pickup.Base);
                toSpawn--;
            }
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            if (ev.Player.GetSessionVar<bool>(SessionVarType.ITEM_LESS_CLSSS_CHANGE))
                return;
            if (ev.NewRole == RoleType.FacilityGuard)
            {
                ev.Items.Remove(ItemType.GunCOM18);
                this.CallDelayed(
                    .25f,
                    () =>
                    {
                        if (ev.Player.Items.Count >= 8)
                            CustomWeapon.TrySpawn(1, ev.Player.Position, out Pickup pickup);
                        else
                            CustomWeapon.TryGive(ev.Player, 1);
                    },
                    "ChangingRole");
            }
        }
    }
}
