// -----------------------------------------------------------------------
// <copyright file="TaserItem.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Mistaken.API;
using Mistaken.API.Extensions;
using Mistaken.API.GUI;
using Mistaken.CustomItems;
using Mistaken.RoundLogger;
using UnityEngine;

namespace Mistaken.Taser
{
    public partial class TaserHandler
    {
        /// <summary>
        /// USP that applies some effects on target.
        /// </summary>
        public class TaserItem : CustomItem
        {
            /// <summary>
            /// Gives Taser to <paramref name="player"/>.
            /// </summary>
            /// <param name="player">Player to give taser to.</param>
            public static void Give(Player player)
            {
                if (player.Inventory.items.Count < 8)
                {
                    float dur = 501000 + (index++);
                    player.AddItem(new Inventory.SyncItemInfo
                    {
                        durability = dur,
                        id = ItemType.GunUSP,
                    });
                    player.SetSessionVar(SessionVarType.CI_TASER, true);
                }
            }

            /// <inheritdoc cref="CustomItem.CustomItem"/>
            public TaserItem() => this.Register();

            /// <inheritdoc/>
            public override string ItemName => "Taser";

            /// <inheritdoc/>
            public override ItemType Item => ItemType.GunUSP;

            /// <inheritdoc/>
            public override SessionVarType SessionVarType => SessionVarType.CI_TASER;

            /// <inheritdoc/>
            public override int Durability => 501;

            /// <inheritdoc/>
            public override Vector3 Size => TaserHandler.Size;

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
            };

            /// <inheritdoc/>
            public override void OnStartHolding(Player player, Inventory.SyncItemInfo item)
            {
                Instance.RunCoroutine(this.UpdateInterface(player), "Taser.UpdateInterface");
            }

            /// <inheritdoc/>
            public override void Spawn(Vector3 position, float innerDurability = 0f)
            {
                float dur = (this.Durability * 1000) + (index++);
                MapPlus.Spawn(
                    new Inventory.SyncItemInfo
                    {
                        durability = dur,
                        id = ItemType.GunUSP,
                    }, position,
                    Quaternion.identity,
                    this.Size);
            }

            /// <inheritdoc/>
            public override bool OnReload(Player player, Inventory.SyncItemInfo item)
            {
                return false;
            }

            /// <inheritdoc/>
            public override bool OnShoot(Player player, Inventory.SyncItemInfo item, GameObject target, Vector3 position)
            {
                int dur = (int)this.GetInternalDurability(item);
                if (!this.cooldowns.TryGetValue(dur, out DateTime time))
                    this.cooldowns.Add(dur, DateTime.Now);
                if (DateTime.Now < time)
                {
                    player.SetGUI("taserAmmo", PseudoGUIPosition.TOP, "You have <color=yellow>no ammo</color>", 3);
                }
                else
                {
                    if (player.GetEffectActive<CustomPlayerEffects.Scp268>())
                        player.DisableEffect<CustomPlayerEffects.Scp268>();
                    this.cooldowns[dur] = DateTime.Now.AddSeconds(PluginHandler.Instance.Config.TaserHitCooldown);
                    var targetPlayer = Player.Get(target);
                    if (targetPlayer != null)
                    {
                        player.ReferenceHub.weaponManager.RpcConfirmShot(true, player.ReferenceHub.weaponManager.curWeapon);
                        if (targetPlayer.GetSessionVar<bool>(SessionVarType.CI_LIGHT_ARMOR) || targetPlayer.GetSessionVar<bool>(SessionVarType.CI_ARMOR) || targetPlayer.GetSessionVar<bool>(SessionVarType.CI_HEAVY_ARMOR))
                        {
                            RLogger.Log("TASER", "BLOCKED", $"{player.PlayerToString()} hit {targetPlayer.PlayerToString()} but effects were blocked by an armor");
                            return false;
                        }

                        if (targetPlayer.GetSessionVar<bool>(SessionVarType.SPAWN_PROTECT))
                        {
                            RLogger.Log("TASER", "REVERSED", $"{player.PlayerToString()} hit {targetPlayer.PlayerToString()} but effects were reversed because of spawn protect");
                            targetPlayer = player;

                            // return false;
                        }

                        if (targetPlayer.IsHuman)
                        {
                            targetPlayer.EnableEffect<CustomPlayerEffects.Ensnared>(2);
                            targetPlayer.EnableEffect<CustomPlayerEffects.Flashed>(5);
                            targetPlayer.EnableEffect<CustomPlayerEffects.Deafened>(10);
                            targetPlayer.EnableEffect<CustomPlayerEffects.Blinded>(10);
                            targetPlayer.EnableEffect<CustomPlayerEffects.Amnesia>(5);
                            if (targetPlayer.CurrentItemIndex != -1 && !UsableItems.Contains(targetPlayer.CurrentItem.id))
                            {
                                Exiled.Events.Handlers.Player.OnDroppingItem(new Exiled.Events.EventArgs.DroppingItemEventArgs(targetPlayer, targetPlayer.CurrentItem));
                                var pickup = MapPlus.Spawn(targetPlayer.CurrentItem, targetPlayer.Position, Quaternion.identity, Vector3.one);

                                // targetPlayer.DropItem(targetPlayer.CurrentItem);
                                targetPlayer.RemoveItem(targetPlayer.CurrentItem);
                                Exiled.Events.Handlers.Player.OnItemDropped(new Exiled.Events.EventArgs.ItemDroppedEventArgs(targetPlayer, pickup));
                                targetPlayer.CurrentItem = default;
                            }

                            RLogger.Log("TASER", "HIT", $"{player.PlayerToString()} hit {targetPlayer.PlayerToString()}");
                            targetPlayer.Broadcast("<color=yellow>Taser</color>", 10, $"<color=yellow>You have been tased by: {player.Nickname} [{player.Role}]</color>");
                            targetPlayer.SendConsoleMessage($"You have been tased by: {player.Nickname} [{player.Role}]", "yellow");
                            return false;
                        }
                    }
                    else
                    {
                        var colliders = UnityEngine.Physics.OverlapSphere(position, 0.1f);
                        if (colliders != null)
                        {
                            /*foreach (var _item in colliders)
                            {
                                if (!Mistaken.Systems.Misc.DoorHandler.Doors.TryGetValue(_item.gameObject, out var door) || door == null)
                                    continue;
                                door.ServerChangeLock(DoorLockReason.NoPower, true);
                                Instance.CallDelayed(10, () => door.ServerChangeLock(DoorLockReason.NoPower, false), "UnlockDoors");
                                player.ReferenceHub.weaponManager.RpcConfirmShot(true, player.ReferenceHub.weaponManager.curWeapon);
                            }*/
                            player.ReferenceHub.weaponManager.RpcConfirmShot(false, player.ReferenceHub.weaponManager.curWeapon);
                            RLogger.Log("TASER", "HIT", $"{player.PlayerToString()} hit door");
                            return false;
                        }
                    }

                    RLogger.Log("TASER", "HIT", $"{player.PlayerToString()} didn't hit anyone");
                    this.cooldowns[dur] = DateTime.Now.AddSeconds(PluginHandler.Instance.Config.TaserMissCooldown);
                }

                return false;
            }

            /// <inheritdoc/>
            public override void OnStopHolding(Player player, Inventory.SyncItemInfo item)
            {
                player.SetGUI("taser", PseudoGUIPosition.BOTTOM, null);
            }

            /// <inheritdoc/>
            public override void OnForceclass(Player player)
            {
                player.SetGUI("taser", PseudoGUIPosition.BOTTOM, null);
            }

            private readonly Dictionary<int, DateTime> cooldowns = new Dictionary<int, DateTime>();

            private IEnumerator<float> UpdateInterface(Player player)
            {
                yield return Timing.WaitForSeconds(0.5f);
                while (player.CurrentItem.id == ItemType.GunUSP)
                {
                    if (!(player.CurrentItem.durability >= 501000f && player.CurrentItem.durability <= 502000f))
                        break;
                    int dur = (int)this.GetInternalDurability(player.CurrentItem);
                    if (!this.cooldowns.TryGetValue(dur, out DateTime time))
                    {
                        this.cooldowns.Add(dur, DateTime.Now);
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

                    player.SetGUI("taser", PseudoGUIPosition.BOTTOM, $"Trzymasz <color=yellow>Taser</color><br><mspace=0.5em><color=yellow>[<color=green>{bar}</color>]</color></mspace>");
                    yield return Timing.WaitForSeconds(1f);
                }

                player.SetGUI("taser", PseudoGUIPosition.BOTTOM, null);
            }
        }
    }
}
