'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { FaCoffee, FaUserCircle } from 'react-icons/fa';

import Lottie from '#components/base/Lottie';
import Modal from '#components/layout/Modal';
import { getAnimSrc } from '#utils/constants/common';
import UserLogin from '../[restaurant]/_components/Menu/UserLogin';

import './customerLoginSection.scss';

const CustomerLoginSection = () => {
	const router = useRouter();
	const [loginModalOpen, setLoginModalOpen] = useState(false);

	const handleCoffeeShopsClick = () => {
		router.push('/address-selection');
	};

	const handleLoginSuccess = () => {
		setLoginModalOpen(false);
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
						<button
							className="loginButton"
							onClick={() => setLoginModalOpen(true)}
						>
							<FaUserCircle />
							<span>Quick Login</span>
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

			<Modal open={loginModalOpen} setOpen={setLoginModalOpen}>
				<UserLogin setOpen={handleLoginSuccess} />
			</Modal>
		</section>
	);
};

export default CustomerLoginSection;
