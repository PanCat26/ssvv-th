using System.ComponentModel.DataAnnotations;

namespace ssvv_th.Models
{
    public class Member
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number.")]
        [StringLength(30)]
        public string? Phone { get; set; }

        public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    }
}
