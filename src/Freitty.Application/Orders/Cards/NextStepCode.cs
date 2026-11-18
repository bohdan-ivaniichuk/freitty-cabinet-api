namespace Freitty.Application.Orders.Cards;

// What the card's "Next" line is about; the page owns the wording.
public enum NextStepCode
{
    ContinueEditing = 1,
    UploadPhoto = 2,
    ReviewQuantityDelta = 3,
    AwaitingTruck = 4,
    Unloading = 5,
    Loading = 6,
    AwaitingCompletion = 7,
    AwaitingPayment = 8,
    Paid = 9,
    Closed = 10,
}
