import { useState } from 'react';

import { useOrder } from '#components/context/useContext';
import Invoice from '#components/layout/Invoice';
import Modal from '#components/layout/Modal';
import NoContent from '#components/layout/NoContent';
import { TOrder } from '#utils/database/models/order';
import { Spinner } from 'xtreme-ui';

import CustomerHistoryCard from './CustomerHistoryCard';
import './customerOrderHistory.scss';

const CustomerOrderHistory = () => {
	const { orderHistory, historyLoading } = useOrder();
	const [selectedOrder, setSelectedOrder] = useState<TOrder | null>(null);
	const [invoiceOpen, setInvoiceOpen] = useState(false);

	const handleCardClick = (order: TOrder) => {
		setSelectedOrder(order);
		setInvoiceOpen(true);
	};

	if (historyLoading) {
		return (
			<div className="customerOrderHistory">
				<Spinner label="Loading order history..." fullpage />
			</div>
		);
	}

	if (!orderHistory?.length) {
		return (
			<div className="customerOrderHistory">
				<NoContent
					label={"No order history"}
					animationName="GhostNoContent"
				/>
			</div>
		);
	}

	return (
		<>
			<div className="customerOrderHistory">
				<div className="historyList">
					{orderHistory.map((order) => (
						<CustomerHistoryCard
							key={order._id.toString()}
							order={order}
							onClick={() => handleCardClick(order)}
						/>
					))}
				</div>
			</div>
			<Modal open={invoiceOpen} setOpen={setInvoiceOpen}>
				{selectedOrder && <Invoice order={selectedOrder} />}
			</Modal>
		</>
	);
};

export default CustomerOrderHistory;
