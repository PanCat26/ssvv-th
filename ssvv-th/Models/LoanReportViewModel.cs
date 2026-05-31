using System.ComponentModel.DataAnnotations;

namespace ssvv_th.Models
{
    public class LoanReportViewModel
    {
        [Display(Name = "Report Type")]
        public LoanReportType ReportType { get; set; } = LoanReportType.All;

        [Display(Name = "Search")]
        public string? SearchTerm { get; set; }

        public List<LoanReportItem> Items { get; set; } = new List<LoanReportItem>();

        public int TotalLoans => Items.Count;
        public int ActiveLoans => Items.Count(i => i.Status == "Active");
        public int OverdueLoans => Items.Count(i => i.Status == "Overdue");
        public int ReturnedLoans => Items.Count(i => i.Status == "Returned");
    }
}
