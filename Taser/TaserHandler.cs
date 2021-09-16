// -----------------------------------------------------------------------
// <copyright file="TaserHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Interactables.Interobjects.DoorUtils;
using Mistaken.API;
using Mistaken.API.Diagnostics;
using Mistaken.API.Extensions;
using UnityEngine;

namespace Mistaken.Taser
{
    /// <inheritdoc/>
    public partial class TaserHandler : Module
    {
        /// <summary>
        /// Spawns taser in the specified <paramref name="position"/> and returns spawned taser.
        /// </summary>
        /// <param name="position">Position where taser will be spawned.</param>
        /// <returns>Spawned taser as <see cref="Pickup"/>.</returns>
        public static Pickup SpawnTaser(Vector3 position)
        {
            float dur = 501000f + (index++);
            return MapPlus.Spawn(
                new Inventory.SyncItemInfo
                {
                    durability = dur,
                    id = ItemType.GunUSP,
                }, position,
                Quaternion.identity,
                Size);
        }

        /// <inheritdoc cref="Module.Module(Exiled.API.Interfaces.IPlugin{Exiled.API.Interfaces.IConfig})"/>
        public TaserHandler(PluginHandler p)
            : base(p)
        {
            Instance = this;
            new TaserItem();
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

        internal static readonly Vector3 Size = new Vector3(.75f, .75f, .75f);
        internal static readonly HashSet<ItemType> UsableItems = new HashSet<ItemType>()
        {
            ItemType.MicroHID,
            ItemType.Medkit,
            ItemType.Painkillers,
            ItemType.SCP018,
            ItemType.SCP207,
            ItemType.SCP268,
            ItemType.SCP500,
            ItemType.GrenadeFrag,
            ItemType.GrenadeFlash,
            ItemType.Adrenaline,
        };

        private static int index = 1;

        private static TaserHandler Instance { get; set; }

        private void Server_RoundStarted()
        {
            index = 1;
            var initOne = SpawnTaser(Vector3.zero);
            this.CallDelayed(5, () => initOne.Delete(), "RoundStarted");
            var lockers = LockerManager.singleton.lockers.Where(i => i.chambers.Length == 9).ToArray();
            int toSpawn = 1;
            while (toSpawn > 0)
            {
                var locker = lockers[UnityEngine.Random.Range(0, lockers.Length)];
                locker.AssignPickup(SpawnTaser(locker.chambers[UnityEngine.Random.Range(0, locker.chambers.Length)].spawnpoint.position));
                toSpawn--;
            }
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            if (ev.IsEscaped)
                return;
            if (ev.Player.GetSessionVar<bool>(SessionVarType.ITEM_LESS_CLSSS_CHANGE))
                return;
            float dur = 501000f + (index++);
            if (ev.NewRole == RoleType.FacilityGuard)
            {
                ev.Items.Remove(ItemType.GunUSP);
                this.CallDelayed(
                    .25f,
                    () =>
                    {
                        if (ev.Player.Inventory.items.Count >= 8)
                        {
                            MapPlus.Spawn(
                                new Inventory.SyncItemInfo
                                {
                                    durability = dur,
                                    id = ItemType.GunUSP,
                                }, ev.Player.Position,
                                Quaternion.identity,
                                Size);
                        }
                        else
                            ev.Player.Inventory.AddNewItem(ItemType.GunUSP, dur);
                    },
                    "ChangingRole");
            }
        }
    }
}
