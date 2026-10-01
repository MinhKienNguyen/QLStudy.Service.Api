namespace QLStudy.Application.Common.Tenancy
{
    public class CurrentTenant : ICurrentTenant
    {
        public int CenterId { get; private set; } = 1;
        public string CenterCode { get; private set; } = "default";
        public bool IsResolved { get; private set; }

        public void SetTenant(int centerId, string centerCode)
        {
            CenterId = centerId <= 0 ? 1 : centerId;
            CenterCode = string.IsNullOrWhiteSpace(centerCode) ? "default" : centerCode;
            IsResolved = true;
        }
    }
}
