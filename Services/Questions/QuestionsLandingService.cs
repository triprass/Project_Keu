namespace Project_Keu.Services.Questions;

public sealed class QuestionsLandingService
{
    private readonly QuestionGroupsService _questionGroupsService;

    public QuestionsLandingService(QuestionGroupsService questionGroupsService)
    {
        _questionGroupsService = questionGroupsService;
    }

    public async Task PrepareQuestionGroupsAsync()
    {
        await _questionGroupsService.GetQuestionGroupsAsync();
    }
}
