namespace GradualRepair
{
    public sealed class InteractionGesture
    {
        public enum Owner { None, Native, Mod }
        public Owner Current { get; private set; }
        int releaseFrame = -1;
        public bool Route(bool held, bool down, bool candidate, bool working, int frame)
        {
            if (Current == Owner.Native) { if (!held) Current = Owner.None; return true; }
            if (Current == Owner.Mod) { if (!held) { Current = Owner.None; releaseFrame = frame; } return false; }
            if (releaseFrame == frame) return false;
            if (down) { Current = candidate || working ? Owner.Mod : Owner.Native; return Current == Owner.Native; }
            return !working;
        }
        public bool Claim(bool held, bool down, int frame)
        {
            if (Current == Owner.Native) return false;
            if (Current == Owner.Mod) return held;
            if (!held || !down || releaseFrame == frame) return false;
            Current = Owner.Mod; return true;
        }
    }
}
