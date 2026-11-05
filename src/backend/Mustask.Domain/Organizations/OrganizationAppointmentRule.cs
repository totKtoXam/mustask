using Mustask.Domain.Identity;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>What an appointment does to the user's membership once no rule refuses it (BR-RS-8).</summary>
public enum AppointmentEffect
{
    /// <summary>The user holds no membership: a new <c>admin</c> one is added.</summary>
    Add,

    /// <summary>The user is a <c>member</c> or a <c>viewer</c>: the role is raised to <c>admin</c>.</summary>
    Raise,
}

/// <summary>
/// The refusals of BR-RS-6a rows 4b to 6, in their one order (feat-033): the user is not appointable (unknown to the projection
/// or marked inactive), the user is the caller, the user already is an administrator. Pure; the handler reads the facts.
/// </summary>
public static class OrganizationAppointmentRule
{
    /// <exception cref="ConflictException">The first refusal that applies.</exception>
    public static AppointmentEffect Decide(string callerId, string userId, bool appointable, OrganizationRole? currentRole)
    {
        if (!appointable)
        {
            throw new ConflictException(OrganizationErrorCodes.RescueUserUnavailable);
        }

        if (string.Equals(callerId, userId, StringComparison.Ordinal))
        {
            throw new ConflictException(OrganizationErrorCodes.RescueSelfAppointment);
        }

        return currentRole switch
        {
            null => AppointmentEffect.Add,
            OrganizationRole.Admin => throw new ConflictException(OrganizationErrorCodes.RescueAlreadyAdmin),
            _ => AppointmentEffect.Raise,
        };
    }
}
