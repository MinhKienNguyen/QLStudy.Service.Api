namespace QLStudy.Domain.Entities
{
    public interface ITenantScoped
    {
        int CenterId { get; set; }
        Center? Center { get; set; }
    }
}
