namespace FluxForge.Story.Hospital
{
    /// <summary>中心医院访客的原版客户工厂与采购名单。</summary>
    internal static class HospitalProcurementClients
    {
        internal const string Visitor01Id = "fluxforge_hospital_visitor_01";
        internal const string Visitor02Id = "fluxforge_hospital_visitor_02";
        internal const string Visitor03Id = "fluxforge_hospital_visitor_03";
        internal const string Visitor04Id = "fluxforge_hospital_visitor_04";
        internal static string DisplayName
        {
            get { return ModHelper.GetLocalized("fluxforge", "npc.hospital_visitor.name"); }
        }

        internal static readonly string[] Identifiers =
        {
            Visitor01Id, Visitor02Id, Visitor03Id, Visitor04Id,
        };

        internal static void Register()
        {
            StoreClientListDict.storeClientDict.Add(Visitor01Id, CreateVisitor01);
            StoreClientListDict.storeClientDict.Add(Visitor02Id, CreateVisitor02);
            StoreClientListDict.storeClientDict.Add(Visitor03Id, CreateVisitor03);
            StoreClientListDict.storeClientDict.Add(Visitor04Id, CreateVisitor04);
        }

        internal static void Unregister()
        {
            for (int i = 0; i < Identifiers.Length; i++)
            {
                StoreClientListDict.storeClientDict.Remove(Identifiers[i]);
            }
            StoreClientListDict.ClearDisplayNameCache();
        }

        private static StoreClient CreateVisitor01()
        {
            return Create(Visitor01Id, "npc_hospital_visitor_01", HospitalVisitorTexts.Visitor01);
        }

        private static StoreClient CreateVisitor02()
        {
            return Create(Visitor02Id, "npc_hospital_visitor_02", HospitalVisitorTexts.Visitor02);
        }

        private static StoreClient CreateVisitor03()
        {
            return Create(Visitor03Id, "npc_hospital_visitor_03", HospitalVisitorTexts.Visitor03);
        }

        private static StoreClient CreateVisitor04()
        {
            return Create(Visitor04Id, "npc_hospital_visitor_04", HospitalVisitorTexts.Visitor04);
        }

        private static StoreClient Create(string identifier, string spriteKey, string[] greetings)
        {
            StoreClient client = new StoreClient();
            client.identifier = identifier;
            client.displayName = DisplayName;
            client.spriteName = spriteKey;
            client.clientFaction = HospitalReputation.FactionId;
            client.clientIntent = StoreClient.ClientIntent.BUY;
            client.IsPersonalConsumption = false;
            client.CanExposeChem = true;
            client.SetBudget(RNG.GetRandomInt(250, 300) + StoreStation.GetDayCounter() * 2);
            client.clientBuyingTagList.Add("ALL_TYPE");

            Dialogue line = client.mainDialogue;
            for (int i = 0; i < greetings.Length; i++)
            {
                line.SetText(DisplayName, greetings[i]);
                if (i < greetings.Length - 1)
                {
                    line = line.NextDialogue();
                }
            }

            client.AddBasicDialog();
            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, "fluxforge");
            return client;
        }
    }
}
