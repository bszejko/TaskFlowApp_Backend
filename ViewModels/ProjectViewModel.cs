namespace TaskFlow.ViewModels // Adjust the namespace based on its location
{
    public class ProjectViewModel
    {
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public DateTime Deadline { get; set; }
        public List<string> Members { get; set; }
        public string CreatedBy { get; set; }
    }
}
