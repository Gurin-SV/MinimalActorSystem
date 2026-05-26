namespace MinimalActorSystem;

/// <summary>
/// Указывает, что для актора должна быть автоматически сгенерирована диспетчеризация писем.
/// Применяется к partial-классу, наследующему от <see cref="Actor"/>.
/// Генератор ищет в классе private-методы с сигнатурой
/// <c>ValueTask On{Тип}({Тип} letter)</c> и создаёт переопределение
/// <see cref="Actor.OnLetter"/> со switch по всем найденным типам писем.
/// Если атрибут отсутствует, актор должен вручную переопределить OnLetter.
/// <para><b>Важно:</b> класс ДОЛЖЕН быть объявлен с модификатором <c>partial</c>, иначе генерация кода не удастся.</para>
/// </summary>
/// <remarks>
/// Enables source-generated message dispatch. Looks for private methods like OnXxx(Xxx letter)
/// and generates an override of Actor.OnLetter with a switch over all found letter types.
/// The class must be partial; otherwise code generation fails.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ActorLetterHandlerAttribute
    : Attribute
{
}
