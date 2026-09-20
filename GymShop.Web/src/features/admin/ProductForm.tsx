import { useState } from 'react'
import type { Category } from '../../api/types'
import { ProductImage } from '../catalog/ProductImage'
import { PRODUCT_LIMITS, type ProductField, type ProductFormErrors, type ProductFormValues, validateProduct } from './productFormValidation'

export function ProductForm({ mode, values: initialValues, categories, busy, serverErrors = {}, onSubmit }: {
  mode: 'create' | 'edit'
  values: ProductFormValues
  categories: Category[]
  busy: boolean
  serverErrors?: ProductFormErrors
  onSubmit(values: ProductFormValues): void
}) {
  const [values, setValues] = useState(initialValues)
  const [clientErrors, setClientErrors] = useState<ProductFormErrors>({})
  const errors = { ...serverErrors, ...clientErrors }
  const change = (field: ProductField, value: string) => { setValues(current => ({ ...current, [field]: value })); setClientErrors(current => ({ ...current, [field]: undefined })) }
  const fieldError = (field: ProductField) => errors[field] ? <small className="field-error" id={`${field}-error`}>{errors[field]}</small> : null
  return <form className="product-form" noValidate onSubmit={event => {
    event.preventDefault()
    if (busy) return
    const nextErrors = validateProduct(values)
    setClientErrors(nextErrors)
    if (Object.keys(nextErrors).length === 0) onSubmit(values)
  }}>
    <div className="product-form-fields">
      <label>Nombre<input aria-label="Nombre" name="name" value={values.name} maxLength={PRODUCT_LIMITS.name} aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? 'name-error' : undefined} onChange={event => change('name', event.target.value)} />{fieldError('name')}</label>
      <label>Descripción<textarea aria-label="Descripción" name="description" value={values.description} maxLength={PRODUCT_LIMITS.description} aria-invalid={Boolean(errors.description)} aria-describedby={errors.description ? 'description-error' : undefined} onChange={event => change('description', event.target.value)} />{fieldError('description')}</label>
      <div className="product-form-row">
        <label>Precio<input aria-label="Precio" name="price" type="number" min="0.01" step="0.01" value={values.price} aria-invalid={Boolean(errors.price)} aria-describedby={errors.price ? 'price-error' : undefined} onChange={event => change('price', event.target.value)} />{fieldError('price')}</label>
        <label>Stock<input aria-label="Stock" name="stock" type="number" min="0" step="1" value={values.stock} aria-invalid={Boolean(errors.stock)} aria-describedby={errors.stock ? 'stock-error' : undefined} onChange={event => change('stock', event.target.value)} />{fieldError('stock')}</label>
      </div>
      <label>Categoría<select aria-label="Categoría" name="categoryId" value={values.categoryId} aria-invalid={Boolean(errors.categoryId)} aria-describedby={errors.categoryId ? 'categoryId-error' : undefined} onChange={event => change('categoryId', event.target.value)}><option value="">Seleccionar categoría</option>{categories.map(category => <option key={category.id} value={category.id}>{category.name}{'isActive' in category && category.isActive === false ? ' (inactiva, categoría actual)' : ''}</option>)}</select>{fieldError('categoryId')}</label>
      <label>URL de imagen<input aria-label="URL de imagen" name="imageUrl" type="url" value={values.imageUrl} maxLength={PRODUCT_LIMITS.imageUrl} placeholder="https://… o /images/…" aria-invalid={Boolean(errors.imageUrl)} aria-describedby={errors.imageUrl ? 'imageUrl-error' : undefined} onChange={event => change('imageUrl', event.target.value)} />{fieldError('imageUrl')}</label>
      {mode === 'edit' && <label className="product-active"><input type="checkbox" checked={values.isActive} onChange={event => setValues(current => ({ ...current, isActive: event.target.checked }))} /> Producto activo</label>}
    </div>
    <aside className="product-form-preview"><span>Vista previa</span><div><ProductImage src={values.imageUrl.trim() && !errors.imageUrl ? values.imageUrl.trim() : null} alt={values.name.trim() || 'Nuevo producto'} /></div><small>Si la imagen no puede cargarse, se mostrará el reemplazo visual de la tienda.</small></aside>
    <div className="product-form-actions"><button className="primary" type="submit" disabled={busy || categories.length === 0}>{busy ? 'Guardando…' : mode === 'create' ? 'Crear producto' : 'Guardar cambios'}</button></div>
  </form>
}
