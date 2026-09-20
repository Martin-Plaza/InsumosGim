import { Link } from 'react-router-dom'
import type { Product } from '../../api/types'
import { money, storefront } from '../../config/storefront'
import { ProductImage } from './ProductImage'

export function ProductCard({ product, onAdd }: { product: Product; onAdd?(product: Product): void }) {
  return <article className={`product-card commercial-card ${product.stock < 1 ? 'out-of-stock' : ''}`}>
    <Link className="product-image" to={`/catalogo/${product.id}`} aria-label={`Ver ${product.name}`}>
      <ProductImage src={product.imageUrl} alt={product.name} />
      {product.category && <span className="category-badge">{product.category.name}</span>}
      {product.stock < 1 && <span className="stock-badge">Sin stock</span>}
    </Link>
    <div className="product-card-body">
      <small>{product.stock > 0 ? `${product.stock} disponibles` : 'Temporalmente sin stock'}</small>
      <h3><Link to={`/catalogo/${product.id}`}>{product.name}</Link></h3>
      <p>{product.description || storefront.copy.productFallback}</p>
      <div className="price-row"><strong>{money(product.price)}</strong>{onAdd
        ? <button disabled={product.stock < 1} onClick={() => onAdd(product)}>{product.stock > 0 ? 'Agregar' : 'Sin stock'}</button>
        : <Link className="card-link" to={`/catalogo/${product.id}`}>{storefront.copy.productAction} →</Link>}
      </div>
    </div>
  </article>
}
