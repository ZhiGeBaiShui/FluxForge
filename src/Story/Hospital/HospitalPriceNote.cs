namespace FluxForge.Story.Hospital
{
    /// <summary>
    /// 医院价格纸条工厂。
    /// 复用原版 handnote：标题、正文、署名、字体和便签 UI 都由原版负责。
    /// </summary>
    internal static class HospitalPriceNote
    {
        internal const string Title = "上层区中心医院采购价目";
        internal const string Sign = "薇拉";
        internal const int Font = 2;

        internal static GameItem Create(string body)
        {
            GameItem note = PreBuiltItemHelper.CreateHandnote(
                body,
                Title,
                Sign,
                Font);

            note.name = Title;
            note.shortDescription = "医院合同的结算方式。";
            note.unitValue = 0L;
            return note;
        }
    }
}
