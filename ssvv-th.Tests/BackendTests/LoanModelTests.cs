using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ssvv_th.Models;
using Xunit;

namespace ssvv_th.Tests.BackendTests
{
    public class LoanModelTests
    {
        [Fact]
        public void LoanModel_ValidData_PassesValidation()
        {
            // Arrange
            var loan = new Loan
            {
                BookId = 1,
                MemberId = 1,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            };

            var context = new ValidationContext(loan, null, null);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(loan, context, results, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Theory]
        [InlineData(0, "Returned")] // ReturnDate is set today
        [InlineData(-1, "Overdue")]  // DueDate was yesterday, ReturnDate null
        [InlineData(1, "Active")]    // DueDate is tomorrow, ReturnDate null
        public void LoanModel_StatusCalculation_ReturnsExpectedStatus(int dueDateOffset, string expectedStatus)
        {
            // Arrange
            var loan = new Loan
            {
                BookId = 1,
                MemberId = 1,
                LoanDate = DateTime.Today.AddDays(-10),
                DueDate = DateTime.Today.AddDays(dueDateOffset)
            };

            if (expectedStatus == "Returned")
            {
                loan.ReturnDate = DateTime.Today;
            }

            // Act
            var actualStatus = loan.Status;

            // Assert
            Assert.Equal(expectedStatus, actualStatus);
        }
    }
}
