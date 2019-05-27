namespace Shuttle.Recall.Tests
{
	public class OrderItem
	{
		public string Product { get; set; }
		public double Quantity { get; set; }
		public double Cost { get; set; }

		public double Total()
		{
			return Quantity*Cost;
		}
	}
}