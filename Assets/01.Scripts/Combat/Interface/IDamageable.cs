namespace MySecondWorld.Combat
{
    // 데미지를 받을 수 있는 대상이 구현해야 하는 계약
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}