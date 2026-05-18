namespace MinimalActorSystem;

/// <summary>
/// Указывает, что для актора должна быть автоматически сгенерирована диспетчеризация писем.
/// Применяется к partial-классу, наследующему от <see cref="Actor"/>.
/// Генератор ищет в классе private-методы с сигнатурой
/// <c>ValueTask On{Тип}({Тип} letter)</c> и создаёт переопределение
/// <see cref="Actor.OnLetter"/> со switch по всем найденным типам писем.
/// Если атрибут отсутствует, актор должен вручную переопределить OnLetter.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ActorLetterHandlerAttribute
    : Attribute
{
}
