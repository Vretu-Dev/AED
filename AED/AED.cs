﻿using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Mirror;
using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;
using YamlDotNet.Serialization;
using Light = Exiled.API.Features.Toys.Light;

namespace AED
{
    [CustomItem(ItemType.Medkit)]
    public class AED : CustomItem
    {
        public Color glowColor = new Color32(255, 0, 0, 10);

        private readonly Dictionary<Pickup, Light> ActiveLights = new();
        private readonly Dictionary<ushort, int> ChargesLeft = new();
        private readonly Dictionary<int, float> LastUsedTime = new();
        private readonly Dictionary<Ragdoll, int> ShocksOnRagdoll = new();

        [YamlIgnore]
        public override ItemType Type { get; set; } = ItemType.Medkit;
        public override uint Id { get; set; } = 999;
        public override string Name { get; set; } = "<color=red>AED</color>";
        public override string Description { get; set; } = "<color=red>A</color>utomated <color=red>E</color>xternal <color=red>D</color>efibrillator";
        public override float Weight { get; set; } = 1f;
        public override Vector3 Scale { get; set; } = new Vector3(0.5f, 0.5f, 0.5f);

        public float ReviveRadius { get; set; } = 2f;
        public float RevivedHealth { get; set; } = 50f;
        public int NumberOfShocks { get; set; } = 1;
        public int ShockToRevive { get; set; } = 1;
        public float ChargingTime { get; set; } = 15f;

        public string ReviverHint { get; set; } = "<color=#00E5FF>You revived the player {target}</color>";
        public string RevivedHint { get; set; } = "<color=#FFDD00>You were revived using an <color=red>AED</color></color>";
        public string ShockProgressHint { get; set; } = "Shock <color=yellow>{applied}</color>/<color=yellow>{required}</color> to revive {target}";
        public string ChargingHint { get; set; } = "<color=red>AED</color> charging... <color=yellow>{percent}%</color>";
        public string FailUsed { get; set; } = "You can’t use <color=red>AED</color> here.";
        public string ShocksLeft { get; set; } = "<color=red>AED</color> charges: <color=yellow>{left}</color>/<color=yellow>{max}</color>";

        public override SpawnProperties SpawnProperties { get; set; } = new()
        {
            Limit = 3,
            LockerSpawnPoints = new()
            {
                new()
                {
                    Chance = 25,
                    Type = LockerType.Misc,
                    UseChamber = true,
                    Offset = Vector3.zero,
                },
            },
        };

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.UsingItem += OnUsingItem;
            Exiled.Events.Handlers.Map.PickupAdded += AddGlow;
            Exiled.Events.Handlers.Map.PickupDestroyed += RemoveGlow;
            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.UsingItem -= OnUsingItem;
            Exiled.Events.Handlers.Map.PickupAdded -= AddGlow;
            Exiled.Events.Handlers.Map.PickupDestroyed -= RemoveGlow;
            CleanupAllGlow();
            base.UnsubscribeEvents();
        }

        protected override void OnWaitingForPlayers()
        {
            base.OnWaitingForPlayers();
            ChargesLeft.Clear();
            LastUsedTime.Clear();
            ShocksOnRagdoll.Clear();
        }

        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);

            if (!ChargesLeft.ContainsKey(item.Serial))
                ChargesLeft[item.Serial] = Mathf.Max(0, NumberOfShocks);

            int left = Mathf.Max(0, ChargesLeft[item.Serial]);

            if (left > 1)
                player.ShowHint(ShocksLeft.Replace("{left}", left.ToString()).Replace("{max}", NumberOfShocks.ToString()), 3f);
        }

        private void OnUsingItem(UsingItemEventArgs ev)
        {
            if (ev.Item == null || !Check(ev.Player.CurrentItem))
                return;

            ev.IsAllowed = false;

            if (Extensions.IsNotSafeArea(ev.Player.Position, ev.Player.CurrentRoom))
            {
                ev.Player.ShowHint(FailUsed, 3f);
                return;
            }
            
            ushort serial = ev.Item.Serial;

            if (!ChargesLeft.ContainsKey(serial))
                ChargesLeft[serial] = Mathf.Max(0, NumberOfShocks);

            if (ChargesLeft[serial] <= 0)
            {
                ev.Player.RemoveItem(ev.Item);
                ChargesLeft.Remove(serial);
                LastUsedTime.Remove(serial);
            }

            if (LastUsedTime.TryGetValue(serial, out float lastUse))
            {
                float since = Time.time - lastUse;

                if (since < ChargingTime)
                {
                    float progress = Mathf.Clamp01(since / ChargingTime);
                    int percent = Mathf.RoundToInt(progress * 100f);

                    ev.Player.ShowHint(ChargingHint.Replace("{percent}", percent.ToString()), 1.5f);
                    return;
                }
            }

            Ragdoll nearest = null;

            float maxDistSqr = ReviveRadius * ReviveRadius;
            float nearestDistSqr = float.MaxValue;

            foreach (var ragdoll in Ragdoll.List)
            {
                if (ragdoll == null || ragdoll.Owner == null || ragdoll.Owner.Role is not SpectatorRole || ragdoll.Owner.IsScp || ragdoll.Role.IsScp())
                    continue;

                float dSqr = (ragdoll.Position - ev.Player.Position).sqrMagnitude;

                if (dSqr <= maxDistSqr && dSqr < nearestDistSqr)
                {
                    nearest = ragdoll;
                    nearestDistSqr = dSqr;
                }
            }

            if (nearest == null || nearest.Owner == null)
                return;

            ChargesLeft[serial] = Mathf.Max(0, ChargesLeft[serial] - 1);
            LastUsedTime[serial] = Time.time;

            if (!ShocksOnRagdoll.TryGetValue(nearest, out int applied))
                applied = 0;

            applied++;
            ShocksOnRagdoll[nearest] = applied;

            var revidedPlayer = nearest.Owner;

            if (applied >= Mathf.Max(1, ShockToRevive))
            {
                var revivePos = nearest.Position + Vector3.up * 0.1f;

                revidedPlayer.Role.Set(nearest.Role, SpawnReason.Respawn, RoleSpawnFlags.None);
                revidedPlayer.Position = revivePos;
                revidedPlayer.Health = Mathf.Max(1f, RevivedHealth);

                nearest.Destroy();
                ShocksOnRagdoll.Remove(nearest);

                // Hints
                ev.Player.ShowHint(ReviverHint.Replace("{target}", revidedPlayer.Nickname));
                revidedPlayer.ShowHint(RevivedHint, 5f);
            }
            else
            {
                ev.Player.ShowHint(ShockProgressHint
                    .Replace("{applied}", applied.ToString())
                    .Replace("{required}", Mathf.Max(1, ShockToRevive).ToString())
                    .Replace("{target}", revidedPlayer.Nickname), 3f);
            }

            if (ChargesLeft[serial] <= 0)
            {
                ev.Player.RemoveItem(ev.Item);
                ChargesLeft.Remove(serial);
                LastUsedTime.Remove(serial);
            }
        }

        public void AddGlow(PickupAddedEventArgs ev)
        {
            if (Check(ev.Pickup) && ev.Pickup.PreviousOwner != null)
            {
                if (ev.Pickup?.Base?.gameObject == null)
                    return;

                TryGet(ev.Pickup, out CustomItem ci);
                Log.Debug($"Pickup is CI: {ev.Pickup.Serial} | {ci?.Id} | {ci?.Name}");

                var light = Light.Create(ev.Pickup.Position);
                light.Color = glowColor;
                light.Intensity = 0.7f;
                light.Range = 0.25f;
                light.ShadowType = LightShadows.None;

                light.Base.gameObject.transform.SetParent(ev.Pickup.Base.gameObject.transform);

                ActiveLights[ev.Pickup] = light;
            }
        }

        public void RemoveGlow(PickupDestroyedEventArgs ev)
        {
            if (!Check(ev.Pickup))
                return;

            if (ev.Pickup == null || ev.Pickup?.Base?.gameObject == null)
                return;

            if (TryGet(ev.Pickup.Serial, out CustomItem ci) && ci != null)
            {
                if (!ActiveLights.ContainsKey(ev.Pickup))
                    return;

                var light = ActiveLights[ev.Pickup];

                if (light != null && light.Base != null)
                {
                    NetworkServer.Destroy(light.Base.gameObject);
                }

                ActiveLights.Remove(ev.Pickup);
            }
        }

        private void CleanupAllGlow()
        {
            foreach (var kv in ActiveLights)
            {
                var light = kv.Value;
                if (light != null && light.Base != null)
                {
                    try { NetworkServer.Destroy(light.Base.gameObject); } catch { /* ignore */ }
                }
            }
            ActiveLights.Clear();
        }
    }
}