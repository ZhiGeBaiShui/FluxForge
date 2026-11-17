namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalSynaprestGreetingEdge : StoryEdge
    {
        internal HospitalSynaprestGreetingEdge() : base(null, null) { }
        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting3));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting4));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting5));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting6));
        }
    }

    internal sealed class HospitalSynaprestAcceptEdge : StoryEdge
    {
        internal HospitalSynaprestAcceptEdge() : base(HospitalSynaprestTexts.AcceptOption, "question") { }
        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept1));
            player.Do(delegate
            {
                HospitalSynaprestStock.GiveInitial();
                HospitalSynaprestState.Start();
            });
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept3));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept4));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept5));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept6));
        }
    }

    internal sealed class HospitalSynaprestDeclineEdge : StoryEdge
    {
        internal HospitalSynaprestDeclineEdge() : base(HospitalSynaprestTexts.DeclineOption, "question") { }
        internal override void Execute(StorySequence player)
        {
            HospitalSynaprestState.Close();
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Decline));
        }
    }
}
