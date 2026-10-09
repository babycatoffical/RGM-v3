namespace RGM.Events
{
    public static class Scp079Events
    {
        public static void OnRecontained(Exiled.Events.EventArgs.Scp079.RecontainedEventArgs ev)
        {
            ev.Player.Kill("재격리 버튼에 의해 격리되었습니다.");
        }
    }
}
