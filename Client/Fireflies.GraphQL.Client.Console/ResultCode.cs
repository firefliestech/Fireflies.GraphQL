namespace Fireflies.GraphQL.Client.Console; 

public enum ResultCode {
    Success,
    GenerationFailed,
    PathDoesNotExist,
    ProjectFileNotFound,
    ClientAlreadyExists,
    FailedToDownloadSchema,
    GraphQLDirectoryNotFound,
    ClientDirectoryNotFound
}