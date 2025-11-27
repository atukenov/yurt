import { TOrder } from '#utils/database/models/order';

import './customerHistoryCard.scss';

const CustomerHistoryCard = (props: TCustomerHistoryCard) => {
	const { order, onClick } = props;
	
	const customerName = `${order?.customer?.fname} ${order?.customer?.lname}`;
	const orderDate = new Date((order as any).createdAt).toLocaleDateString('en-US', {
		month: 'short',
		day: 'numeric',
		year: 'numeric',
	});
	const orderTime = new Date((order as any).createdAt).toLocaleTimeString('en-US', {
		hour: '2-digit',
		minute: '2-digit',
	});

	return (
		<div className='customerHistoryCard' onClick={onClick}>
			<div className='cardHeader'>
				<div className='orderInfo'>
					<p className='orderDate'>{orderDate}</p>
					<p className='orderTime'>{orderTime}</p>
				</div>
				<div className={`orderStatus ${order.state}`}>
					{order.state === 'complete' ? 'Completed' : 'Rejected'}
				</div>
			</div>
			<div className='cardBody'>
				<p className='customerName'>{customerName}</p>
				<p className='itemCount'>{order.products?.length || 0} items</p>
			</div>
			<div className='cardFooter'>
				<p className='orderTotal'>
					<span className='label'>Total:</span>
					<span className='amount tenge'>{order.orderTotal + order.taxTotal}</span>
				</p>
			</div>
		</div>
	);
};

export default CustomerHistoryCard;

type TCustomerHistoryCard = {
	order: TOrder;
	onClick: () => void;
};
