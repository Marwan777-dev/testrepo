using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Polymorphic base for the per-type question payload stored in <c>questions.type_payload</c>
/// (jsonb, research.md §5). The concrete record is chosen by the <c>$type</c> discriminator; a
/// value converter (<c>QuestionTypePayloadConverter</c>, T062) serialises it with
/// System.Text.Json. Per-type field validity is enforced by <c>QuestionValidator</c> (T075)
/// before persist — the records themselves are plain data.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ScalePayload), "scale")]
[JsonDerivedType(typeof(InputFieldPayload), "input_field")]
[JsonDerivedType(typeof(SingleSelectPayload), "single_select")]
[JsonDerivedType(typeof(MultiSelectPayload), "multi_select")]
[JsonDerivedType(typeof(YesNoPayload), "yes_no")]
[JsonDerivedType(typeof(MatrixPayload), "matrix")]
[JsonDerivedType(typeof(RankingPayload), "ranking")]
[JsonDerivedType(typeof(KpiPayload), "kpi")]
public abstract record QuestionTypePayload;
