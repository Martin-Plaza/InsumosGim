import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../api/gymshop'
import type { Product } from '../../api/types'
import { useCart } from '../cart/useCart'
import { filterAndSortProducts, type AvailabilityFilter, type CatalogSort } from './catalogFilters'
import { ProductImage } from './ProductImage'

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)
const numberOrNull = (value: string) => value === '' ? null : Number(value)

export function Catalog() {
  const cart = useCart()
  const [products, setProducts] = useState<Product[]>([])
  const [query, setQuery] = useState('')
  const [availability, setAvailability] = useState<AvailabilityFilter>('all')
  const [minPrice, setMinPrice] = useState('')
  const [maxPrice, setMaxPrice] = useState('')
  const [sort, setSort] = useState<CatalogSort>('relevance')
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const load = () => {
    setLoading(true); setLoadError('')
    api.products(false).then(result => setProducts(result.filter(product => product.isActive))).catch(() => setLoadError('No pudimos cargar el catálogo.')).finally(() => setLoading(false))
  }
  useEffect(load, [])

  const invalidRange = minPrice !== '' && maxPrice !== '' && Number(minPrice) > Number(maxPrice)
  const visible = useMemo(() => invalidRange ? [] : filterAndSortProducts(products, {
    query, availability, minPrice: numberOrNull(minPrice), maxPrice: numberOrNull(maxPrice), sort,
  }), [availability, invalidRange, maxPrice, minPrice, products, query, sort])

  return <section className="catalog-page">
    <div className="section-title"><div><p className="eyebrow">CATÁLOGO ACTIVO</p><h1>Elegí tu próximo desafío</h1></div><span>{visible.length} de {products.length} productos</span></div>
    <div className="catalog-toolbar">
      <label className="catalog-search">Buscar<input type="search" placeholder="Nombre o descripción" value={query} onChange={event => setQuery(event.target.value)} /></label>
      <label>Disponibilidad<select value={availability} onChange={event => setAvailability(event.target.value as AvailabilityFilter)}><option value="all">Todos</option><option value="available">Con stock</option><option value="unavailable">Sin stock</option></select></label>
      <fieldset><legend>Rango de precio</legend><input aria-label="Precio mínimo" type="number" min="0" placeholder="Mínimo" value={minPrice} onChange={event => setMinPrice(event.target.value)} /><input aria-label="Precio máximo" type="number" min="0" placeholder="Máximo" value={maxPrice} onChange={event => setMaxPrice(event.target.value)} /></fieldset>
      <label>Ordenar<select value={sort} onChange={event => setSort(event.target.value as CatalogSort)}><option value="relevance">Relevancia</option><option value="price-asc">Menor precio</option><option value="price-desc">Mayor precio</option><option value="name-asc">Nombre A–Z</option><option value="name-desc">Nombre Z–A</option></select></label>
    </div>
    {invalidRange && <div className="error" role="alert">El precio mínimo no puede superar al máximo.</div>}
    {loading ? <div className="empty">Cargando catálogo…</div> : loadError ? <div className="empty"><p>{loadError}</p><button onClick={load}>Reintentar</button></div> : products.length === 0 ? <div className="empty">No hay productos activos disponibles.</div> : visible.length === 0 ? <div className="empty">No encontramos productos con esos filtros.</div> : <div className="catalog-grid">{visible.map(product => <article className={`product-card catalog-card ${product.stock < 1 ? 'out-of-stock' : ''}`} key={product.id}>
      <Link className="product-image" to={`/catalogo/${product.id}`}><ProductImage src={product.imageUrl} alt={product.name} />{product.stock < 1 && <span className="stock-badge">Sin stock</span>}</Link>
      <div><small>{product.stock > 0 ? `${product.stock} disponibles` : 'Temporalmente sin stock'}</small><h2><Link to={`/catalogo/${product.id}`}>{product.name}</Link></h2><p>{product.description || 'Equipamiento GymShop.'}</p><div className="price-row"><strong>{money(product.price)}</strong><button disabled={product.stock < 1} onClick={() => void cart.add(product, 1)}>{product.stock > 0 ? 'Agregar' : 'Sin stock'}</button></div></div>
    </article>)}</div>}
  </section>
}
