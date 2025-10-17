using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Mirror;
using Org.BouncyCastle.Ocsp;
using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YamlDotNet.Serialization;
using Light = Exiled.API.Features.Toys.Light;

namespace AED
{
    [CustomItem(ItemType.Medkit)]
    public class AED : CustomItem
    {
        public Color glowColor = new Color32(255, 0, 0, 10);
        private readonly Dictionary<Pickup, Light> ActiveLights = new();

        [YamlIgnore]
        public override ItemType Type { get; set; } = ItemType.Medkit;
        public override uint Id { get; set; } = 999;
        public override string Name { get; set; } = "<color=red>AED</color>";
        public override string Description { get; set; } = "<color=red>A</color>utomated <color=red>E</color>xternal <color=red>D</color>efibrillator";
        public override float Weight { get; set; } = 1f;
        public override Vector3 Scale { get; set; } = new Vector3(0.5f, 0.5f, 0.5f);
        public float ReviveRadius { get; set; } = 2f;
        public float RevivedHealth { get; set; } = 50f;
        public string ReviverHint { get; set; } = "<color=#00E5FF>You revived the player {target}</color>";
        public string RevivedHint { get; set; } = "<color=#FFDD00>You were revived using an <color=red>AED</color>.</color>";
        public string FailUsed { get; set; } = "You can’t use<color=red>AED</color> here.";
        
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
        private void OnUsingItem(UsingItemEventArgs ev)
        {
            if (ev.Item == null || !Check(ev.Player.CurrentItem))
                return;

            if (Extensions.IsNotSafeArea(ev.Player.Position, ev.Player.CurrentRoom))
            {
                ev.IsAllowed = false;
                ev.Player.ShowHint(FailUsed, 3f);
                return;
            }

            ev.IsAllowed = false;

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

            var revidedPlayer = nearest.Owner;
            var revivePos = nearest.Position + Vector3.up * 0.1f;

            revidedPlayer.Role.Set(nearest.Role, SpawnReason.Respawn, RoleSpawnFlags.None);
            revidedPlayer.Position = revivePos;
            revidedPlayer.Health = Mathf.Max(1f, RevivedHealth);

            nearest.Destroy();

            ev.Player.RemoveItem(ev.Item);

            // Hints
            ev.Player.ShowHint(ReviverHint.Replace("{target}", revidedPlayer.Nickname));
            revidedPlayer.ShowHint(RevivedHint, 5f);
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