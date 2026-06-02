namespace Entities.Concrete
{
    public class Material
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? ImagePath { get; set; }
        public int RepairItemId { get; set; }
        public virtual RepairItem RepairItem { get; set; }
    }
}