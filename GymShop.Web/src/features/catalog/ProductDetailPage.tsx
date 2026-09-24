import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { api } from '../../api/gymshop'
import type { Product } from '../../api/types'
import { money, storefront } from '../../config/storefront'
import { useCart } from '../cart/useCart'
import { ProductCard } from './ProductCard'
import { ProductImage } from './ProductImage'
import { QuantitySelector } from './QuantitySelector'

export function ProductDetailPage() {
  const { productId } = useParams(); const cart = useCart(); const [product, setProduct] = useState<Product | null>(null); const [related, setRelated] = useState<Product[]>([]); const [quantity, setQuantity] = useState(1); const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  useEffect(() => { const id = Number(productId); setLoading(true); setError(''); setRelated([]); if (!Number.isInteger(id) || id < 1) { setError('El producto solicitado no es válido.'); setLoading(false); return }
    api.product(id).then(result => { setProduct(result); setQuantity(result.stock > 0 ? 1 : 0); const categorySlug = result.category?.slug; if (!categorySlug) return
      return api.products(false).then(products => { if (Array.isArray(products)) setRelated(products.filter(item => item.id !== result.id && item.category?.slug === categorySlug).slice(0, 3)) }).catch(() => undefined) }).catch(value => setError(value instanceof ApiError && value.status === 404 ? 'El producto no existe o ya no está activo.' : 'No pudimos cargar el producto.')).finally(() => setLoading(false)) }, [productId])
  if (loading) return <div className="product-detail-skeleton" aria-label="Cargando producto"><div /><div /></div>
  if (error || !product) return <div className="empty state-card"><h1>No encontramos ese producto</h1><p>{error}</p><Link className="link-button primary" to="/catalogo">Volver al catálogo</Link></div>
  return <section className="product-detail-page"><nav className="breadcrumbs" aria-label="Ruta de navegación"><Link to="/">Inicio</Link><span>/</span><Link to="/catalogo">Catálogo</Link>{product.category && <><span>/</span><Link to={`/catalogo?categoria=${product.category.slug}`}>{product.category.name}</Link></>}<span>/</span><span aria-current="page">{product.name}</span></nav>
    <div className="product-detail-layout"><div className="product-detail-image"><ProductImage src={product.imageUrl} alt={product.name} /></div><div className="product-detail-copy">{product.category && <Link className="detail-category" to={`/catalogo?categoria=${product.category.slug}`}>{product.category.name}</Link>}<h1>{product.name}</h1>{product.description?.trim() && <p className="product-description">{product.description}</p>}<strong className="product-detail-price">{money(product.price)}</strong><p className={product.stock > 0 ? 'in-stock' : 'no-stock'}><span aria-hidden="true">●</span> {product.stock > 0 ? `Disponible · ${product.stock} unidades` : 'Producto sin stock'}</p>{product.stock > 0 && <div className="product-buy"><QuantitySelector value={quantity} max={product.stock} onChange={setQuantity} /><button className="primary" onClick={() => void cart.add(product, quantity)}>{storefront.copy.addToCart}</button></div>}<div className="purchase-benefits"><p><b>Envíos a todo el país</b><span>Coordinamos la entrega de tu equipo.</span></p><p><b>Compra protegida</b><span>Tu pedido y tu cuenta, siempre seguros.</span></p></div></div></div>
    {related.length > 0 && <section className="related-products" aria-labelledby="related-title"><p className="eyebrow">{storefront.copy.relatedEyebrow}</p><div className="section-title"><h2 id="related-title">{storefront.copy.relatedTitle}</h2><Link to={`/catalogo?categoria=${product.category?.slug}`}>Ver categoría →</Link></div><div className="product-grid">{related.map(item => <ProductCard key={item.id} product={item} onAdd={selected => void cart.add(selected, 1)} />)}</div></section>}
  </section>
}
