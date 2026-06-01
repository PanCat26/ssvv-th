using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ssvv_th.Models;
using Xunit;

namespace ssvv_th.Tests.BlackBoxTests
{
    public class BookMemberBbtTests
    {
        private static IList<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }

        private static bool HasErrorFor(IEnumerable<ValidationResult> results, string property)
            => results.Any(r => r.MemberNames.Contains(property));

        private static Book ValidBook() => new Book
        {
            Title = "Clean Code",
            Author = "Robert C. Martin",
            ISBN = "9780132350884",
            AvailableCopies = 3
        };

        private static Member ValidMember() => new Member
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "0712345678"
        };

        [Fact]
        public void Book_AllFieldsValid_PassesValidation()
        {
            Assert.Empty(Validate(ValidBook()));
        }

        [Fact]
        public void Book_MissingTitle_FailsValidation()
        {
            var book = ValidBook();
            book.Title = string.Empty;

            Assert.True(HasErrorFor(Validate(book), nameof(Book.Title)));
        }

        [Fact]
        public void Book_MissingAuthor_FailsValidation()
        {
            var book = ValidBook();
            book.Author = string.Empty;

            Assert.True(HasErrorFor(Validate(book), nameof(Book.Author)));
        }

        [Fact]
        public void Book_MissingIsbn_FailsValidation()
        {
            var book = ValidBook();
            book.ISBN = string.Empty;

            Assert.True(HasErrorFor(Validate(book), nameof(Book.ISBN)));
        }

        [Theory]
        [InlineData(200, true)]
        [InlineData(201, false)]
        public void Book_TitleLengthBoundary_IsValidated(int length, bool expectedValid)
        {
            var book = ValidBook();
            book.Title = new string('a', length);

            Assert.Equal(expectedValid, !HasErrorFor(Validate(book), nameof(Book.Title)));
        }

        [Theory]
        [InlineData(20, true)]
        [InlineData(21, false)]
        public void Book_IsbnLengthBoundary_IsValidated(int length, bool expectedValid)
        {
            var book = ValidBook();
            book.ISBN = new string('1', length);

            Assert.Equal(expectedValid, !HasErrorFor(Validate(book), nameof(Book.ISBN)));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(5, true)]
        [InlineData(-1, false)]
        public void Book_AvailableCopiesBoundary_IsValidated(int copies, bool expectedValid)
        {
            var book = ValidBook();
            book.AvailableCopies = copies;

            Assert.Equal(expectedValid, !HasErrorFor(Validate(book), nameof(Book.AvailableCopies)));
        }

        [Fact]
        public void Member_AllFieldsValid_PassesValidation()
        {
            Assert.Empty(Validate(ValidMember()));
        }

        [Fact]
        public void Member_MissingName_FailsValidation()
        {
            var member = ValidMember();
            member.Name = string.Empty;

            Assert.True(HasErrorFor(Validate(member), nameof(Member.Name)));
        }

        [Fact]
        public void Member_MissingEmail_FailsValidation()
        {
            var member = ValidMember();
            member.Email = string.Empty;

            Assert.True(HasErrorFor(Validate(member), nameof(Member.Email)));
        }

        [Theory]
        [InlineData("john.doe@example.com", true)]
        [InlineData("first.last@sub.domain.org", true)]
        [InlineData("not-an-email", false)]
        [InlineData("missing-at-sign.com", false)]
        public void Member_EmailFormat_IsValidated(string email, bool expectedValid)
        {
            var member = ValidMember();
            member.Email = email;

            Assert.Equal(expectedValid, !HasErrorFor(Validate(member), nameof(Member.Email)));
        }

        [Theory]
        [InlineData(150, true)]
        [InlineData(151, false)]
        public void Member_NameLengthBoundary_IsValidated(int length, bool expectedValid)
        {
            var member = ValidMember();
            member.Name = new string('n', length);

            Assert.Equal(expectedValid, !HasErrorFor(Validate(member), nameof(Member.Name)));
        }

        [Fact]
        public void Member_NullPhone_IsAllowed()
        {
            var member = ValidMember();
            member.Phone = null;

            Assert.False(HasErrorFor(Validate(member), nameof(Member.Phone)));
        }

        [Fact]
        public void Member_InvalidPhone_FailsValidation()
        {
            var member = ValidMember();
            member.Phone = "not a phone";

            Assert.True(HasErrorFor(Validate(member), nameof(Member.Phone)));
        }
    }
}
