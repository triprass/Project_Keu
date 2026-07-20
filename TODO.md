# TODO - DetailPertanyaan dynamic question and answer from AdminDashboardV2 selection

- [ ] Update `Pages/DetailPertanyaan.cshtml.cs`
  - [ ] Load answer from `tb_t_answer` by `questionId`
  - [ ] Set default answer text when null: "Belum ada respon dari PIC Keuangan"
  - [ ] Set close/open status flag based on answer availability
- [ ] Update `Pages/DetailPertanyaan.cshtml`
  - [ ] Bind `.detail-answer-card` to dynamic answer text
  - [ ] Change `topic-status-open` text to `CLOSE` when answer exists
- [ ] Run build test (`dotnet build`)
- [ ] Report final summary and verification status
