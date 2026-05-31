using System.ComponentModel.DataAnnotations;

namespace ssvv_th.Models
{
    public class LoanReportViewModel
    {
        [Display(Name = "From")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [Display(Name = "To")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        [Display(Name = "Report Type")]
        public LoanReportType ReportType { get; set; } = LoanReportType.All;

        [Display(Name = "Search")]
        public string? SearchTerm { get; set; }

        public List<LoanReportItem> Items { get; set; } = new List<LoanReportItem>();

        public int TotalBooksLoaned => Items.Count;
        public int UniqueBooksLoaned => Items.Select(i => i.BookTitle).Distinct().Count();
        public int ActiveLoans => Items.Count(i => i.Status == "Active");
        public int OverdueLoans => Items.Count(i => i.Status == "Overdue");
        public int ReturnedLoans => Items.Count(i => i.Status == "Returned");
    }
}
