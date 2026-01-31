namespace com.aqua.netlogic.sim.game.entity
{
    public sealed class Entity
    {
        public int Id { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; private set; }
        public int HasteTicksRemaining { get; set; }
        public int DashCooldownTicksRemaining { get; set; }

        public Entity(int id, int x, int y, int hp = 100)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = hp;
            HasteTicksRemaining = 0;
            DashCooldownTicksRemaining = 0;
        }

        public void MoveBy(int dx, int dy) => X += dx;

        public void Damage(int amount)
        {
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }
    }
}
