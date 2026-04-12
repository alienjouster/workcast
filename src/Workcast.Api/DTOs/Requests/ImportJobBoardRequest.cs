using Workcast.Api.DTOs.Exchange;

namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for importing a job board configuration from a portable exchange file.
/// Inherits the full <see cref="BoardExchangeDto"/> shape so that community-boards JSON files
/// can be posted to the import endpoint without transformation.
/// </summary>
public record ImportJobBoardRequest : BoardExchangeDto;
