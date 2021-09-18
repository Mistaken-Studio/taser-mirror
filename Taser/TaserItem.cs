// -----------------------------------------------------------------------
// <copyright file="TaserItem.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.CustomItems.API.Features;
using Exiled.CustomItems.API.Spawn;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.BasicMessages;
using MEC;
using Mistaken.API;
using Mistaken.API.Extensions;
using Mistaken.API.GUI;
using Mistaken.RoundLogger;
using UnityEngine;

namespace Mistaken.Taser
{
    /// <summary>
    /// Com18 that applies some effects on target.
    /// </summary>
    public class TaserItem : CustomWeapon
    {
        /*/// <inheritdoc/>
        public override SessionVarType SessionVarType => SessionVarType.CI_TASER;

        /// <inheritdoc/>
        public override Upgrade[] Upgrades => new Upgrade[]
        {
                new Upgrade
                {
                    Chance = 100,
                    Durability = null,
                    Input = ItemType.GunUSP,
                    KnobSetting = Scp914.Scp914Knob.OneToOne,
                },
        };*/

        /// <inheritdoc/>
        public override uint Id { get; set; } = 1;

        /// <inheritdoc/>
        public override string Name { get; set; } = "Taser";

        /// <inheritdoc/>
        public override string Description { get; set; } = "A Taser";

        /// <inheritdoc/>
        public override ItemType Type { get; set; } = ItemType.GunCOM18;

        /// <inheritdoc/>
        public override float Weight { get; set; } = 0.1f;

        /// <inheritdoc/>
        public override SpawnProperties SpawnProperties { get; set; }

        /// <inheritdoc/>
        public override Modifiers Modifiers { get; set; } = new Modifiers(0, 0, 0);

        /// <inheritdoc/>
        public override byte ClipSize { get; set; } = 1;

        /// <inheritdoc/>
        public override float Damage { get; set; } = 5;

        /// <inheritdoc/>
        public override Pickup Spawn(Vector3 position, Item item)
        {
            var pickup = base.Spawn(position, item);
            pickup.Scale = Size;
            if (this.cooldowns.TryGetValue(item.Serial, out DateTime value))
            {
                this.cooldowns.Add(pickup.Serial, value);
                this.cooldowns.Remove(item.Serial);
            }

            return pickup;
        }

        /// <inheritdoc/>
        public override Pickup Spawn(Vector3 position)
        {
            var pickup = base.Spawn(position);
            pickup.Scale = Size;
            var firearmPickup = pickup.Base as FirearmPickup;
            firearmPickup.Status = new FirearmStatus(1, FirearmStatusFlags.Cocked, firearmPickup.Status.Attachments);
            firearmPickup.NetworkStatus = firearmPickup.Status;
            return pickup;
        }

        internal static readonly Vector3 Size = new Vector3(.75f, .75f, .75f);

        /// <inheritdoc/>
        protected override void ShowSelectedMessage(Player player)
        {
            TaserHandler.Instance.RunCoroutine(this.UpdateInterface(player), "Taser.UpdateInterface");
        }

        /// <inheritdoc/>
        protected override void OnReloading(Exiled.Events.EventArgs.ReloadingWeaponEventArgs ev)
        {
            ev.IsAllowed = false;
            base.OnReloading(ev);
        }

        /// <inheritdoc/>
        protected override void ShowPickedUpMessage(Player player)
        {
            player.SetGUI("taserpickedupmessage", PseudoGUIPosition.MIDDLE, "Podniosłeś <color=yellow>Taser</color>", 2f);
        }

        /// <inheritdoc/>
        protected override void OnShooting(Exiled.Events.EventArgs.ShootingEventArgs ev)
        {
            if (!this.cooldowns.TryGetValue(ev.Shooter.CurrentItem.Serial, out DateTime time))
                this.cooldowns.Add(ev.Shooter.CurrentItem.Serial, DateTime.Now);
            if (DateTime.Now < time)
            {
                ev.Shooter.SetGUI("taserammo", PseudoGUIPosition.TOP, "Nie masz <color=yellow>amunicji</color>", 2);
                ev.IsAllowed = false;
                return;
            }
            else
            {
                (ev.Shooter.CurrentItem as Exiled.API.Features.Items.Firearm).Ammo += 1;
                if (ev.Shooter.GetEffectActive<CustomPlayerEffects.Invisible>())
                    ev.Shooter.DisableEffect<CustomPlayerEffects.Invisible>();
                this.cooldowns[ev.Shooter.CurrentItem.Serial] = DateTime.Now.AddSeconds(PluginHandler.Instance.Config.TaserHitCooldown);
                Player targetPlayer = (RealPlayers.List.Where(x => x.NetworkIdentity.netId == ev.TargetNetId).Count() > 0) ? RealPlayers.List.First(x => x.NetworkIdentity.netId == ev.TargetNetId) : null;
                if (targetPlayer != null)
                {
                    ev.Shooter.Connection.Send<RequestMessage>(new RequestMessage(0, RequestType.Hitmarker), 0);
                    if (targetPlayer.Items.Select(x => x.Type).Any(x => x == ItemType.ArmorLight || x == ItemType.ArmorCombat || x == ItemType.ArmorHeavy))
                    {
                        RLogger.Log("TASER", "BLOCKED", $"{ev.Shooter.PlayerToString()} hit {targetPlayer.PlayerToString()} but effects were blocked by an armor");
                        return;
                    }

                    if (targetPlayer.GetSessionVar<bool>(SessionVarType.SPAWN_PROTECT))
                    {
                        RLogger.Log("TASER", "REVERSED", $"{ev.Shooter.PlayerToString()} hit {targetPlayer.PlayerToString()} but effects were reversed because of spawn protect");
                        targetPlayer = ev.Shooter;
                        return;
                    }

                    if (targetPlayer.IsHuman)
                    {
                        targetPlayer.EnableEffect<CustomPlayerEffects.Ensnared>(2);
                        targetPlayer.EnableEffect<CustomPlayerEffects.Flashed>(5);
                        targetPlayer.EnableEffect<CustomPlayerEffects.Deafened>(10);
                        targetPlayer.EnableEffect<CustomPlayerEffects.Blinded>(10);
                        targetPlayer.EnableEffect<CustomPlayerEffects.Amnesia>(5);
                        if (targetPlayer.CurrentItem != null && !TaserHandler.UsableItems.Contains(targetPlayer.CurrentItem.Type))
                        {
                            Exiled.Events.Handlers.Player.OnDroppingItem(new Exiled.Events.EventArgs.DroppingItemEventArgs(targetPlayer, targetPlayer.CurrentItem.Base));
                            var pickup = MapPlus.Spawn(targetPlayer.CurrentItem.Type, targetPlayer.Position, Quaternion.identity, Vector3.one);
                            pickup.ItemSerial = targetPlayer.CurrentItem.Serial;

                            targetPlayer.DropItem(targetPlayer.CurrentItem);
                            targetPlayer.RemoveItem(targetPlayer.CurrentItem);
                            targetPlayer.CurrentItem = default;
                        }

                        RLogger.Log("TASER", "HIT", $"{ev.Shooter.PlayerToString()} hit {targetPlayer.PlayerToString()}");
                        targetPlayer.Broadcast("<color=yellow>Taser</color>", 10, $"<color=yellow>You have been tased by: {ev.Shooter.Nickname} [{ev.Shooter.Role}]</color>");
                        targetPlayer.SendConsoleMessage($"You have been tased by: {ev.Shooter.Nickname} [{ev.Shooter.Role}]", "yellow");
                        return;
                    }
                }
                else
                {
                    UnityEngine.Physics.Raycast(ev.Shooter.Position, ev.Shooter.CameraTransform.forward, out RaycastHit hitinfo);
                    if (hitinfo.collider != null)
                    {
                        if (!TaserHandler.Doors.TryGetValue(hitinfo.collider.gameObject, out var door) || door == null)
                        {
                            RLogger.Log("TASER", "HIT", $"{ev.Shooter.PlayerToString()} didn't hit anyone");
                            this.cooldowns[ev.Shooter.CurrentItem.Serial] = DateTime.Now.AddSeconds(PluginHandler.Instance.Config.TaserMissCooldown);
                            return;
                        }

                        ev.Shooter.Connection.Send<RequestMessage>(new RequestMessage(0, RequestType.Hitmarker), 0);
                        door.ChangeLock(DoorLockType.NoPower);
                        RLogger.Log("TASER", "HIT", $"{ev.Shooter.PlayerToString()} hit door");
                        TaserHandler.Instance.CallDelayed(10, () => door.ChangeLock(DoorLockType.NoPower), "UnlockDoors");
                        return;
                    }
                }
            }
        }

        private readonly Dictionary<ushort, DateTime> cooldowns = new Dictionary<ushort, DateTime>();

        private IEnumerator<float> UpdateInterface(Player player)
        {
            yield return Timing.WaitForSeconds(0.1f);
            while (this.Check(player.CurrentItem))
            {
                if (!this.cooldowns.TryGetValue(player.CurrentItem.Serial, out DateTime time))
                {
                    this.cooldowns.Add(player.CurrentItem.Serial, DateTime.Now);
                    time = DateTime.Now;
                }

                var diff = ((PluginHandler.Instance.Config.TaserHitCooldown - (time - DateTime.Now).TotalSeconds) / PluginHandler.Instance.Config.TaserHitCooldown) * 100;
                string bar = string.Empty;
                for (int i = 1; i <= 20; i++)
                {
                    if (i * (100 / 20) > diff)
                        bar += "<color=red>|</color>";
                    else
                        bar += "|";
                }

                player.SetGUI("taserholding", PseudoGUIPosition.BOTTOM, $"Trzymasz <color=yellow>Taser</color><br><mspace=0.5em><color=yellow>[<color=green>{bar}</color>]</color></mspace>");
                yield return Timing.WaitForSeconds(1f);
            }

            player.SetGUI("taserholding", PseudoGUIPosition.BOTTOM, null);
        }
    }
}
