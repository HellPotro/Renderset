using Microsoft.AspNetCore.Identity;

namespace Renderset.Web.Identity;

/// <summary>
/// Mensajes de Identity en español: los de serie salen en inglés y se
/// enseñan tal cual en las páginas de contraseña e invitación.
/// </summary>
public sealed class SpanishIdentityErrorDescriber
    : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "No se ha podido completar la operación.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Otra persona ha cambiado la cuenta a la vez. Vuelve a intentarlo.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "La contraseña actual no es correcta.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "El enlace no es válido o ha caducado.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "El correo no es válido.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "El correo no es válido como nombre de usuario.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Ya hay una cuenta con ese correo.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Ya hay una cuenta con ese correo.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "La cuenta ya tiene contraseña.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "El bloqueo no está activado para esta cuenta.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"La contraseña tiene que tener al menos {length} caracteres.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"La contraseña tiene que tener al menos {uniqueChars} caracteres distintos.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña tiene que tener algún símbolo.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "La contraseña tiene que tener algún número.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "La contraseña tiene que tener alguna minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "La contraseña tiene que tener alguna mayúscula.");

    private static IdentityError Error(
        string code,
        string description) =>
        new() { Code = code, Description = description };
}
