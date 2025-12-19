import { useState, useRef, useEffect } from 'react';

import Button from '#components/base/Button';
import { TOrder } from '#utils/database/models/order';

import './invoice.scss';

type InvoiceBillItemProps = {
	name: string;
	taxPercent?: number;
	amount: number;
};

const InvoiceBillItem = (props: InvoiceBillItemProps) => {
	return (<div className='invoiceBillItem'>
		<p className='billName'>{props.name + (props.taxPercent ? ` (${props.taxPercent}%)` : '')}</p>
		<p className='billAmount tenge'>{props.amount}</p>
	</div>);
};

type InvoiceOrderItem = {
	name: string;
	price: number;
	quantity: number;
};

type InvoiceTax = {
	name: string;
	value: number;
	calculatedTax: number;
};

type ExtendedOrder = TOrder & {
	products: InvoiceOrderItem[];
	total: number;
	taxes: InvoiceTax[];
	invoiceNumber: string;
};

const Invoice = (props: TInvoiceProps) => {
	const [taxList, setTaxList] = useState<InvoiceTax[]>([]);
	const invoiceRef = useRef<any>(null);
	const [orderList, setOrderList] = useState<InvoiceOrderItem[]>([]);
	const [subTotal, setSubTotal] = useState(0);
	const [grandTotal, setGrandTotal] = useState(0);

	const downloadPDF = () => {
		invoiceRef.current?.save?.();
	};

	useEffect(() => {
		if (props.order) {
			const extOrder = props.order as unknown as ExtendedOrder;
			setOrderList(extOrder.products || []);
			setSubTotal(extOrder.total || 0);
			setGrandTotal(extOrder.orderTotal || 0);
			setTaxList(extOrder.taxes || []);
		}
	}, [props.order]);

	return (
		<div className='invoiceWrapper'>
			<div className='invoice'>
				<div className='invoiceItems'>
					<h6 className='invoiceItemsHeading'>Your Order Summary</h6>
					<hr />
					<h6 className='invoiceHeadingDetails' style={{ textAlign: 'left' }}>
						Invoice Number: <span>{(props.order as unknown as ExtendedOrder).invoiceNumber}</span>
					</h6>
					<h6 className='invoiceHeadingDetails' style={{ textAlign: 'left' }}>
						Customer Name: <span>{props?.order?.customer?.fname} {props?.order?.customer?.lname}</span>
					</h6>
					<hr />
					{
						orderList.map((item, key) => (
							<div className='invoiceItemCard' key={key}>
								<p className='invoiceItemName'>{item.name}</p>
								<div className='invoiceItemPrice'>
									<p className='tenge'>{item.price}<span>✕</span>{item.quantity}</p>
									<p className='tenge'>{item.price * item.quantity}</p>
								</div>
							</div>
						))
					}
				</div>
				<div className='invoiceBill'>
					<InvoiceBillItem name='Sub Total' amount={subTotal} />
					<div className='invoiceTaxes'>
						{
							taxList?.map?.((taxName, key) => {
								return (<InvoiceBillItem key={key} name={taxName.name} taxPercent={taxName.value} amount={taxName.calculatedTax} />);
							})
						}
					</div>
					<InvoiceBillItem name='Grand Total' amount={grandTotal} />
				</div>
				<Button className='invoiceDownload' icon='f354' onClick={downloadPDF} />
			</div>
		</div>
	);
};

export default Invoice;

type TInvoiceProps = {
	order: TOrder;
};
