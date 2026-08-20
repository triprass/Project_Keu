namespace Project_Keu.Services.Notifications;

public enum NotificationKind
{
    /// <summary>Pegawai mengirim pertanyaan baru; pengelola yang diberi tahu.</summary>
    QuestionCreated,

    /// <summary>Pengelola menyimpan jawaban; penanya yang diberi tahu.</summary>
    QuestionAnswered
}

/// <summary>
/// Satu pekerjaan pemberitahuan. Yang dititipkan hanya id pertanyaan, bukan entity-nya:
/// pekerjaan dijalankan di luar request sehingga DbContext yang memuatnya sudah dibuang,
/// dan datanya dibaca ulang di lingkup miliknya sendiri.
/// </summary>
public sealed record NotificationJob(NotificationKind Kind, Guid QuestionId)
{
    public static NotificationJob QuestionCreated(Guid questionId) =>
        new(NotificationKind.QuestionCreated, questionId);

    public static NotificationJob QuestionAnswered(Guid questionId) =>
        new(NotificationKind.QuestionAnswered, questionId);
}
