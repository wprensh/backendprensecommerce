using PrensEcommerce.Api.Features.Account;
using PrensEcommerce.Api.Features.Reviews;

namespace PrensEcommerce.Api.Tests.Account;

public sealed class AccountValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Review_rating_must_be_between_one_and_five(int rating) =>
        Assert.False(new UpsertMyReview.Validator().Validate(new UpsertMyReview.Command(rating, null, "Bien")).IsValid);

    [Fact]
    public void Review_needs_a_comment() =>
        Assert.False(new UpsertMyReview.Validator().Validate(new UpsertMyReview.Command(5, "Título", " ")).IsValid);

    [Fact]
    public void Valid_review_passes() =>
        Assert.True(new UpsertMyReview.Validator().Validate(new UpsertMyReview.Command(5, null, "Excelente")).IsValid);

    [Fact]
    public void New_password_must_differ_and_have_eight_characters()
    {
        var validator = new ChangePassword.Validator();
        Assert.False(validator.Validate(new ChangePassword.Command("Secreto123", "Secreto123")).IsValid);
        Assert.False(validator.Validate(new ChangePassword.Command("Secreto123", "corta")).IsValid);
        Assert.True(validator.Validate(new ChangePassword.Command("Secreto123", "OtraClave456")).IsValid);
    }

    [Theory]
    [InlineData("+57 300 123 4567", true)]
    [InlineData("(601) 555-0000", true)]
    [InlineData("llámame", false)]
    public void Phone_accepts_only_phone_characters(string phone, bool valid) =>
        Assert.Equal(valid, new UpdateProfile.Validator().Validate(new UpdateProfile.Command("Ana", phone, false)).IsValid);
}
