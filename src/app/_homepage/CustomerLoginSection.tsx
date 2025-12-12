'use client';

import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { FaCoffee } from 'react-icons/fa';

import './customerLoginSection.scss';

const CustomerLoginSection = () => {
	const router = useRouter();

	const handleCoffeeShopsClick = () => {
		router.push('/address-selection');
	};

	return (
		<section className="customerLoginSection" id="homepage-customer">
			<div className="customerLoginContainer">
				<div className="customerLoginCard">
					<div className="header">
						<h3>Welcome to <span className='logo'>YURT</span></h3>
						<h4>Your favorite coffee is just a tap away</h4>
					</div>
					<div className="actionButtons">
						<button
							className="coffeeShopsButton"
							onClick={handleCoffeeShopsClick}
						>
							<FaCoffee />
							<span>Browse Coffee Shops</span>
						</button>
					</div>
					<div className="adminLinkContainer">
						<p className="adminText">
							Are you a restaurant owner?{' '}
							<Link href="/admin/login" className="adminLink">
								Admin Login
							</Link>
						</p>
					</div>
				</div>
			</div>
		</section>
	);
};

export default CustomerLoginSection;
