using ssvv_th.Models;

namespace ssvv_th.Services
{
    public sealed record LoanValidationError(string PropertyName, string ErrorMessage);

    public sealed class LoanOperationResult
    {
        private LoanOperationResult(bool succeeded, Loan? loan, IReadOnlyCollection<LoanValidationError> errors)
        {
            Succeeded = succeeded;
            Loan = loan;
            Errors = errors;
        }

        public bool Succeeded { get; }

        public Loan? Loan { get; }

        public IReadOnlyCollection<LoanValidationError> Errors { get; }

        public static LoanOperationResult Success(Loan loan)
        {
            return new LoanOperationResult(true, loan, Array.Empty<LoanValidationError>());
        }

        public static LoanOperationResult Failure(params LoanValidationError[] errors)
        {
            return new LoanOperationResult(false, null, errors);
        }

        public static LoanOperationResult Failure(IEnumerable<LoanValidationError> errors)
        {
            return new LoanOperationResult(false, null, errors.ToArray());
        }
    }
}
