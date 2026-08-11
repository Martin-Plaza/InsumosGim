import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { api } from '../../api/gymshop'
import type { Product } from '../../api/types'
import { useCart } from '../cart/useCart'
import { ProductImage } from './ProductImage'

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)

export function ProductDetailPage() {
  const { productId } = useParams()
  const cart = useCart()
  const [product, setProduct] = useState<Product | null>(null)
  const [quantity, setQuantity] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const id = Number(productId)
    if (!Number.isInteger(id) || id < 1) { setError('El producto solicitado no es válido.'); setLoading(false); return }
    api.product(id).then(result => { setProduct(result); setQuantity(result.stock > 0 ? 1 : 0) }).catch(value => setError(value instanceof ApiError && value.status === 404 ? 'El producto no existe o ya no está activo.' : 'No pudimos cargar el producto.')).finally(() => setLoading(false))
  }, [productId])

  if (loading) return <div className="empty">Cargando producto…</div>
  if (error || !product) return <div className="empty"><p>{error}</p><Link to="/catalogo">Volver al catálogo</Link></div>

  return <section className="product-detail-page">
    <Link className="back-link" to="/catalogo">← Volver al catálogo</Link>
    <div className="product-detail-layout"><div className="product-detail-image"><ProductImage src={product.imageUrl} alt={product.name} /></div><div className="product-detail-copy">
      <p className="eyebrow">PRODUCTO</p><h1>{product.name}</h1><p className="product-description">{product.description || 'Equipamiento GymShop.'}</p><strong className="product-detail-price">{money(product.price)}</strong>
      <p className={product.stock > 0 ? 'in-stock' : 'no-stock'}>{product.stock > 0 ? `${product.stock} unidades disponibles` : 'Producto sin stock'}</p>
      {product.stock > 0 && <div className="product-buy"><label>Cantidad<input type="number" min="1" max={product.stock} value={quantity} onChange={event => setQuantity(Math.min(product.stock, Math.max(1, Number(event.target.value))))} /></label><button className="primary" onClick={() => void cart.add(product, quantity)}>Agregar al carrito</button></div>}
    </div></div>
  </section>
}
