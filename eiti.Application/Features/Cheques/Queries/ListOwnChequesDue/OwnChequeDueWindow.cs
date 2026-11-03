namespace eiti.Application.Features.Cheques.Queries.ListOwnChequesDue;

// Qué vencimientos se avisan en un día de negocio: los de ese día. El lunes suma sábado y domingo, porque el
// fin de semana no entra nadie a EITI. Los feriados no se contemplan.
public static class OwnChequeDueWindow
{
    public static (DateTime From, DateTime To) For(DateTime businessDate)
    {
        var today = businessDate.Date;
        return (today.DayOfWeek == DayOfWeek.Monday ? today.AddDays(-2) : today, today);
    }
}
