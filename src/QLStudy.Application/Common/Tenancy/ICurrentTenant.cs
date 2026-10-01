namespace QLStudy.Application.Common.Tenancy
{
    public interface ICurrentTenant
    {
        int CenterId { get; }
        string CenterCode { get; }
        bool IsResolved { get; }
        void SetTenant(int centerId, string centerCode);
    }
}
