namespace PlotTwist.Domain;

// A rule of the domain was broken by input (an empty plot description, a year range the wrong way round).
// The Api maps it to 400 Bad Request; anything else is a server fault.
public sealed class DomainException(string message) : Exception(message);
