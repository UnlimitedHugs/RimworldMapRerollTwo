namespace Reroll2 {
	public interface IRerollEventReceiver {
		void OnMapRerolled();
		void OnMapStateSet();
		void OnResourceRockMined();
	}
}