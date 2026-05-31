using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ssvv_th.Models
{
    public class Loan
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a book.")]
        [Display(Name = "Book")]
        public int BookId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select a member.")]
        [Display(Name = "Member")]
        public int MemberId { get; set; }

        [Display(Name = "Loan Date")]
        [DataType(DataType.Date)]
        public DateTime LoanDate { get; set; } = DateTime.Today;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

        [Display(Name = "Return Date")]
        [DataType(DataType.Date)]
        public DateTime? ReturnDate { get; set; }

        // Navigation properties
        public Book? Book { get; set; }
        public Member? Member { get; set; }

        [NotMapped]
        public string Status
        {
            get
            {
                if (ReturnDate != null)
                    return "Returned";
                if (DueDate.Date < DateTime.Today)
                    return "Overdue";
                return "Active";
            }
        }
    }
}
