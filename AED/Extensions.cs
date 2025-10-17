using Exiled.API.Enums;
using Exiled.API.Features;
using UnityEngine;

namespace AED
{
    public static class Extensions
    {
        private static bool IsInElevator(Vector3 position)
        {
            return Lift.Get(position) != null;
        }
        private static bool IsPocketDimension(Room room)
        {
            return room != null && room.Type == RoomType.Pocket;
        }
        public static bool IsNotSafeArea(Vector3 position, Room room)
        {
            if (IsPocketDimension(room))
                return true;

            if (IsInElevator(position))
                return true;

            return false;
        }
    }
}
