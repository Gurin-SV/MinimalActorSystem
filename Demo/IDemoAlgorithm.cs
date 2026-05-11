namespace Demo;

public interface IDemoAlgorithm
{
    /// <summary>
    /// Название алгоритма — отображается на вкладке.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Вызывается однократно при первом переходе на вкладку.
    /// Создаёт визуальное представление, инициализирует алгоритм,
    /// размещает контролы на странице.
    /// </summary>
    void Show(TabPage page);

    /// <summary>
    /// Вызывается таймером главной формы ~40 раз в секунду для активной вкладки.
    /// </summary>
    void Update();
}
