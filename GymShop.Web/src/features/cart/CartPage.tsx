import { useNavigate } from 'react-router-dom'
import { session } from '../../auth/session'
import { ProductImage } from '../catalog/ProductImage'
import { useCart } from './useCart'

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)

export function CartPage() {
  const cart = useCart()
  const navigate = useNavigate()
  const continueToCheckout = () => {
    if (!session.user()) {
      navigate('/login', { state: { returnTo: '/checkout', message: 'Iniciá sesión para finalizar la compra. Conservaremos y combinaremos tu carrito.' } })
      return
    }
    navigate('/checkout')
  }

  return <section className="cart-page">
    <div className="section-title"><div><p className="eyebrow">TU SELECCIÓN</p><h1>Carrito</h1></div><strong>{money(cart.total)}</strong></div>
    {cart.notice && <div className="notice" role="status">{cart.notice}</div>}
    {cart.error && <div className="error" role="alert">{cart.error}</div>}
    {cart.loading ? <div className="empty">Cargando carrito…</div> : cart.items.length === 0 ? <div className="empty">Tu carrito está vacío.</div> : <div className="split">
      <div className="cart-list">{cart.items.map(item => <article className="cart-line" key={item.productId}>
        <div className="cart-line-image"><ProductImage src={item.imageUrl} alt={item.productName} /></div>
        <div className="cart-line-info"><h3>{item.productName}</h3><p>{money(item.unitPrice)} · Stock {item.stock}</p><button className="text-button" onClick={() => void cart.remove(item.productId)}>Quitar</button></div>
        <div className="quantity-control"><button aria-label={`Quitar una unidad de ${item.productName}`} disabled={item.quantity <= 1} onClick={() => void cart.update(item.productId, item.quantity - 1)}>−</button><input aria-label={`Cantidad de ${item.productName}`} type="number" min="1" max={item.stock} value={item.quantity} onChange={event => void cart.update(item.productId, Number(event.target.value))} /><button aria-label={`Sumar una unidad de ${item.productName}`} disabled={item.quantity >= item.stock} onClick={() => void cart.update(item.productId, item.quantity + 1)}>+</button></div>
        <strong>{money(item.subtotal)}</strong>
      </article>)}</div>
      <aside className="summary"><h2>Resumen</h2><p>{session.user() ? 'En el siguiente paso confirmarás la dirección y el pago Mock.' : 'Podés armar tu carrito como visitante. Te pediremos iniciar sesión antes de comprar.'}</p><div><span>Total</span><strong>{money(cart.total)}</strong></div><button className="primary" onClick={continueToCheckout}>{session.user() ? 'Continuar al checkout' : 'Ingresar para comprar'}</button><button type="button" onClick={() => void cart.clear()}>Vaciar carrito</button></aside>
    </div>}
  </section>
}
